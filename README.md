# NostreetsInterceptor
#### Http Request Interceptor Class and Attributes For C#


#### Example
```C#
Add elements these to your web.config
<modules>
      <add name="GenericModule" type="NostreetsInterceptor.GenericModule" />
</modules>
```

```C#
using NostreetsInterceptor;

public class ValidatorConfig {
    
    [Validator("login")]
    public void GetUser(HttpAppa) { 
        //... custom logic
    }

}

public class CustomApiController : ApiController { 
    
    //Can intercept any request before hitting endpoint
    [Intercept("login")]
    [HttpGet]
    public HttpResponseMessage Login() { 
        //... custom logic
    }
}
```