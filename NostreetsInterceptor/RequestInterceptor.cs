using NostreetsExtensions;
using NostreetsExtensions.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
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

                List<Tuple<InterceptAttribute, object>> interceptors = new List<Tuple<InterceptAttribute, object>>();
                List<Tuple<ValidatorAttribute, object>> validators = new List<Tuple<ValidatorAttribute, object>>();


                validators = validators.GetObjectsWithAttribute(ClassTypes.Methods);
                interceptors = interceptors.GetObjectsWithAttribute(ClassTypes.Methods);


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
                            object target = ((MethodInfo)validator.Item2).DeclaringType.UnityInstantiate(ExternalContainer());

                            result.Add(new Tuple<string, object, string, MethodInfo>(validator.Item1.Type, target, route, (MethodInfo)validator.Item2));
                        }
                    }
                }

                return result;
            }
        }

        static UnityContainer ExternalContainer()
        {
            MethodInfo methodInfo = (MethodInfo)"UnityConfig.GetContainer".ScanAssembliesForObject(new[] { "NostreetsInterceptor" });

            UnityContainer result = (UnityContainer)methodInfo.Invoke("UnityConfig".ScanAssembliesForObject(new[] { "NostreetsInterceptor" }).Instantiate(), null) ?? null;

            return result;
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
