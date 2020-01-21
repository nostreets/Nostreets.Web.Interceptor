using Castle.Windsor;
using Nostreets.Extensions.Utilities;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Web;
using System.Web.Http;
using Unity;
using System.Linq;
using Nostreets.Extensions.Extend.Basic;
using Nostreets.Extensions.Extend.IOC;

namespace Nostreets.Web.Interceptor
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public class InterceptAttribute : Attribute
    {
        public InterceptAttribute(string id = null, string eventName = null)
        {
            ID = id ?? ID;
            Event = eventName ?? Event;
        }

        public string ID { get; } = "Any";
        public string Event { get; } = "PreRequestHandlerExecute";
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ValidatorAttribute : Attribute
    {
        public ValidatorAttribute(string id = null, string eventName = null)
        {
            ID = id ?? ID;
            Event = eventName ?? Event;
        }

        public string ID { get; } = "Any";
        public string Event { get; } = "PreRequestHandlerExecute";
    }

    static class Linker
    {
        static Linker()
        {
            Methods = GetMethods();
        }

        /*
         Item 1: Specific Type Key
         Item 2: Target Object that contains specified Method
         Item 3: Specific Route
         Item 4: Method to run
         Item 5: Event to run on
             */
        public static List<Tuple<string, object, string, MethodInfo, string>> Methods { get; private set; } = null;

        private static List<Tuple<string, object, string, MethodInfo, string>> GetMethods()
        {
            List<Tuple<string, object, string, MethodInfo, string>> result = new List<Tuple<string, object, string, MethodInfo, string>>();


            List<Tuple<RoutePrefixAttribute, object, Assembly>> prefixes = 
                Basic.GetObjectsWithAttribute<RoutePrefixAttribute>(a => a.FullName.Contains("Nostreets"), ClassTypes.Type);

            List<Tuple<ValidatorAttribute, object, Assembly>> validators = 
                Basic.GetObjectsWithAttribute<ValidatorAttribute>(a => a.FullName.Contains("Nostreets"), ClassTypes.Methods);

            List<Tuple<InterceptAttribute, object, Assembly>> interceptors = 
                Basic.GetObjectsWithAttribute<InterceptAttribute>(a => a.FullName.Contains("Nostreets"), ClassTypes.Methods);

            bool isAMatch(InterceptAttribute interceptor, ValidatorAttribute validator)
            {
                bool isMatch = false;
                string[] seperators = new[] { ",", ", ", " , ", " ,", "  ,  "};

                foreach (string seperator in seperators) {
                    isMatch = interceptor.ID.Contains(",") ? interceptor.ID.Split(",").Any(a => a == validator.ID) : interceptor.ID != validator.ID;
                    if (isMatch)
                        break;
                }

                return isMatch;
            }


            foreach (var validator in validators)
            {
                foreach (var interceptor in interceptors)
                {
                    if (/*interceptor.Item1.ID != validator.Item1.ID*/
                        isAMatch(interceptor.Item1, validator.Item1)) { continue; }

                    string route = null;
                    if (prefixes.Any(a => ((Type)a.Item2).GetMethods().Any(b => b == (MethodInfo)interceptor.Item2)))
                    {
                        Type[] typePrefixes = prefixes.Where(a => ((Type)a.Item2).GetMethods().Where(b => b == (MethodInfo)interceptor.Item2).Count() > 0)
                                                      .Select(a => (Type)a.Item2).ToArray();

                        foreach (Type type in typePrefixes)
                        {
                            string prefix = type.GetCustomAttribute<RoutePrefixAttribute>().Prefix;

                            if (((MethodInfo)interceptor.Item2).GetCustomAttributes<RouteAttribute>().Count() > 0)
                                route = '/' + prefix + '/' + ((MethodInfo)interceptor.Item2).GetCustomAttributes<RouteAttribute>().First().Template;
                        }
                    }
                    else
                        if (((MethodInfo)interceptor.Item2).GetCustomAttributes<RouteAttribute>().Count() > 0)
                            route = '/' + ((MethodInfo)interceptor.Item2).GetCustomAttributes<RouteAttribute>().First().Template;


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
                                    if (((HttpApplication)a).Request != null && ((HttpApplication)a).Request.Path == item.Item3)
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
