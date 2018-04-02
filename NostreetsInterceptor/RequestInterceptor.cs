using Castle.Windsor;
using NostreetsExtensions;
using NostreetsExtensions.Utilities;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Web;
using System.Web.Http;
using Mvc = System.Web.Mvc;
using Unity;
using System.Linq;

namespace NostreetsInterceptor
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public class InterceptAttribute : Attribute
    {
        public InterceptAttribute(string id = null, string eventName = null)
        {
            _id = id ?? _id;
            _event = eventName ?? _event;
        }

        public string ID { get => _id; }
        public string Event { get => _event; }

        private string _id = "Any";
        private string _event = "PreRequestHandlerExecute";

    }

    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ValidatorAttribute : Attribute
    {
        public ValidatorAttribute(string id = null, string eventName = null)
        {
            _id = id ?? _id;
            _event = eventName ?? _event;
        }

        public string ID { get => _id; }
        public string Event { get => _event; }

        private string _id = "Any";
        private string _event = "PreRequestHandlerExecute";

    }

    static class Linker
    {
        static Linker()
        {
            _methods = GetMethods();
        }
        static List<Tuple<string, object, string, MethodInfo, string>> _methods = null;

        /*
         Item 1: Specific Type Key
         Item 2: Target Object that contains specified Method
         Item 3: Specific Route
         Item 4: Method to run
         Item 5: Event to run on
             */
        public static List<Tuple<string, object, string, MethodInfo, string>> Methods { get { return _methods; } }

        private static List<Tuple<string, object, string, MethodInfo, string>> GetMethods()
        {
            List<Tuple<string, object, string, MethodInfo, string>> result = new List<Tuple<string, object, string, MethodInfo, string>>();


            //List<Type> areas = Extend.GetTypesByAttribute<Mvc.RouteAreaAttribute>();

            List<Tuple<RoutePrefixAttribute, object, Assembly>> prefixes = Extend.GetObjectsWithAttribute<RoutePrefixAttribute>(ClassTypes.Type);
            List<Tuple<ValidatorAttribute, object, Assembly>> validators = Extend.GetObjectsWithAttribute<ValidatorAttribute>(ClassTypes.Methods);
            List<Tuple<InterceptAttribute, object, Assembly>> interceptors = Extend.GetObjectsWithAttribute<InterceptAttribute>(ClassTypes.Methods);


            foreach (var validator in validators)
            {
                foreach (var interceptor in interceptors)
                {
                    if (interceptor.Item1.ID != validator.Item1.ID) { continue; }

                    string route = null;
                    if (prefixes.Any(a => ((Type)a.Item2).GetMethods().Any(b => b == (MethodInfo)interceptor.Item2)))
                    {
                        Type[] typePrefixes = prefixes.Where(a => ((Type)a.Item2).GetMethods().Where(b => b == (MethodInfo)interceptor.Item2) != null)
                                                      .Select(a => (Type)a.Item2).ToArray();

                        foreach (Type type in typePrefixes)
                        {
                            string prefix = type.GetCustomAttribute<RoutePrefixAttribute>().Prefix;

                            if (((MethodInfo)interceptor.Item2).GetCustomAttributes<RouteAttribute>() != null)
                                route = prefix + '/' + ((MethodInfo)interceptor.Item2).GetCustomAttributes<RouteAttribute>().Single().Template;
                        }
                    }
                    else
                        if (((MethodInfo)interceptor.Item2).GetCustomAttributes<RouteAttribute>() != null)
                        route = ((MethodInfo)interceptor.Item2).GetCustomAttributes<RouteAttribute>().Single().Template;


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


                        result.Add(new Tuple<string, object, string, MethodInfo, string>(validator.Item1.ID, target, route, (MethodInfo)validator.Item2, validator.Item1.Event));
                    }
                }
            }
            return result;
        }
    }

    public class RequestInterceptor : IHttpModule
    {
        public void Init(HttpApplication app)
        {
            if (Linker.Methods != null && Linker.Methods.Count > 0)
            {
                foreach (Tuple<string, object, string, MethodInfo, string> item in Linker.Methods)
                {
                    try
                    {
                        app.GetEvent(item.Item5)
                            .AddEventHandler(app,
                                new EventHandler((a, b) =>
                                {
                                    if (((HttpApplication)a).Request != null && ((HttpApplication)a).Request.Path.Contains(item.Item3))
                                    {
                                        item.Item4.Invoke(item.Item2, new[] { (HttpApplication)a });
                                    }
                                }
                            )
                        );
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
