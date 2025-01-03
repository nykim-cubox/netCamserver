using Nancy;
using Nancy.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CameraServer
{
    public delegate string OnGetRequestEvent(string uri, DynamicDictionary query);

    public class WebServiceModule : NancyModule
    {
        public static event OnGetRequestEvent OnGetRequest = null;

        public WebServiceModule()
        {
            Get("/{path}", args => {
                var contents = get_service(args.path, this.Request.Query);

                return
                    string.Compare(contents, "PAGE-NOT-FOUND")!=-1?
                    new Response
                    {
                        StatusCode = HttpStatusCode.NotFound,
                    }
                    :
                    new TextResponse(contents)//, "application/json")
                    {
                        StatusCode = HttpStatusCode.OK,
                        Headers = {
                            { "Connection", "close" },
                            { "Access-Control-Allow-Origin", "*" },
                        }
                    };
            });
        }

        private string get_service(string path, DynamicDictionary query)
        {
            if(OnGetRequest != null)
                return OnGetRequest(path, query);

            return string.Format("PAGE-NOT-FOUND: {0}", path);
        }
    }
}
