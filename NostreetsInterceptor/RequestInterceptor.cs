using Castle.Windsor;
using NostreetsExtensions;
using NostreetsExtensions.Utilities;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Web;
using System.Web.Http;
using Unity;

namespace NostreetsInterceptor
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class InterceptAttribute : Attribute
    {
        public InterceptAttribute(string type = "Any")
        {
            _type = type;
        }

        public string Type { get { return _type; } }

        private string _type = "Any";

    }

    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ValidatorAttribute : Attribute
    {
        public ValidatorAttribute(string type = "Any")
        {
            _type = type;
        }

        public string Type { get { return _type; } }

        private string _type = "Any";

    }

    static class Interceptor
    {
        /*
         Item 1: Specific Type Key
         Item 2: Target Object that contains specified Method
         Item 3: Specific Route
         Item 4: Method to run
             */
        public static List<Tuple<string, object, string, MethodInfo>> Methods
        {
            get
            {
                List<Tuple<string, object, string, MethodInfo>> result = new List<Tuple<string, object, string, MethodInfo>>();


                List<Tuple<ValidatorAttribute, object, Assembly>> validators = new List<Tuple<ValidatorAttribute, object, Assembly>>().GetObjectsWithAttribute(ClassTypes.Methods);
                List<Tuple<InterceptAttribute, object, Assembly>> interceptors = new List<Tuple<InterceptAttribute, object, Assembly>>().GetObjectsWithAttribute(ClassTypes.Methods);


                foreach (var validator in validators)
                {
                    foreach (var interceptor in interceptors)
                    {
                        if (interceptor.Item1.Type != validator.Item1.Type) { continue; }

                        string route = null;
                        foreach (Attribute attr in ((MethodInfo)interceptor.Item2).GetCustomAttributes())
                        {
                            if (attr.GetType() == typeof(RouteAttribute))
                            {
                                route = ((RouteAttribute)attr).Template;
                            }
                        }

                        if (route != null)
                        {

                            Type targetType = ((MethodInfo)validator.Item2).DeclaringType;
                            object container = validator.Item3.GetWindsorContainer(),
                                   target = null;

                            if (container == null)
                                container = validator.Item3.GetUnityContainer();

                            bool resovled = (container.GetType().HasInterface<IWindsorContainer>())
                                            ? targetType.TryWindsorResolve((IWindsorContainer)container, out target)
                                            : (container.GetType().HasInterface<IUnityContainer>())
                                            ? targetType.TryUnityResolve((IUnityContainer)container, out target) 
                                            : false;

                            if (!resovled)
                                target = targetType.Instantiate();


                            result.Add(new Tuple<string, object, string, MethodInfo>(validator.Item1.Type, target, route, (MethodInfo)validator.Item2));
                        }
                    }
                }

                return result;
            }
        }

    }

    public class GenericModule : IHttpModule
    {
        public void Init(HttpApplication app)
        {
            if (Interceptor.Methods != null && Interceptor.Methods.Count > 0)
            {
                foreach (Tuple<string, object, string, MethodInfo> item in Interceptor.Methods)
                {
                    try
                    {
                        app.PreRequestHandlerExecute += new EventHandler((a, b) => { if (((HttpApplication)a).Request != null && ((HttpApplication)a).Request.Path.Contains(item.Item3)) { item.Item4.Invoke(item.Item2, new[] { (HttpApplication)a }); } });
                    }
                    catch (Exception)
                    {

                        continue;
                    }
                }
            }
        }

        public void Dispose() { }
    }


}
