/**
* @author RRiber
*
* @date - 23.10.2019. 
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;

namespace Server
{
    static class Method
    {
        public const string Read = "read";
        public const string Create = "create";
        public const string Update = "update";
        public const string Delete = "delete";
        public const string Echo = "echo";

        public static readonly string[] methods = { Read, Create, Update, Delete, Echo };
    }
    public class Response
    {
        public string Status { get; set; }
        public string Body { get; set; }
    }
    public class Request
    {
        public string Method { get; set; }
        public string Path { get; set; }
        public string Date { get; set; }
        public string Body { get; set; }
        
    }

    public class Category
    {
        [JsonPropertyName("cid")]
        public int Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    class Program
    {
        public static List<Category> categories = new List<Category>
    {
      new Category {Id = 1, Name = "Beverages"},
      new Category{Id = 2, Name = "Condiments"},
      new Category{Id = 3, Name = "Confections"}
    };

        static void Main(string[] args)
        {
            new Server(IPAddress.Loopback, 5000);
        }
    }
    class Server
    {
        TcpListener server = null;
        public Server(IPAddress localAddr, int port)
        {
            server = new TcpListener(localAddr, port);
            server.Start();
            StartListener();
        }

        public void StartListener()
        {
            try
            {
                while (true)
                {
                    Console.WriteLine("checking for connection");
                    var client = server.AcceptTcpClient();

                    Thread t = new Thread(new ParameterizedThreadStart(HandleDevice));
                    t.Start(client);
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: ", e);
                server.Stop();
            }
        }

        public void HandleDevice(object obj)
        {
            TcpClient client = (TcpClient)obj;
            var stream = client.GetStream();
            Response response = new Response();

            try
            {
                Console.WriteLine("connected");
                var request = client.ReadRequest();
                var unixTimeStampChecker = UnixTimestamp();
                response.Status = "4 Bad request";

                if (request.Method == null)
                {
                    response.Status = ", missing method";
                }
                if (request.Path == null)
                {
                    response.Status += ", missing resource";
                }
                if (request.Body == null)
                {
                    response.Status += ", missing body";
                }
                if (request.Date == null)
                {
                    response.Status += ", missing date";
                    goto NullCheckEnd;
                }

                if (!(Util.ArrayContains(Method.methods, request.Method)))
                {
                    response.Status += "illegal method";
                    goto NullCheckEnd;
                }
                if (request.Date != null)
                {
                    if (request.Date != unixTimeStampChecker) response.Status += ", illegal date";
                    goto NullCheckEnd;
                }
            
            NullCheckEnd:
                
                if (request.Body != null)
                {
                    try
                    {
                        var categoryFromJson = JsonSerializer.Deserialize<Request>(request.Body);
                    }
                    catch (System.Exception e)
                    {
                        Console.WriteLine("Exception: " +e, e);

                        if (request.Method == "echo")
                        {
                            response.Status = "1 Ok";
                            response.Body = request.Body;
                        }
                        else
                        {
                            response.Status += ", illegal body";
                        }
                    }
                }

                if (request.Path != null && request.Path.Contains("/api"))
                {
                    if (request.Path.Contains("/api/categories"))
                    {
                        Match endpathNr = Regex.Match(request.Path, @"\d+$");  //https://www.dotnetperls.com/regex -//- https://www.computerhope.com/unix/regex-quickref.htm
                        var requestPathId = endpathNr.Value;

                        if (requestPathId == "")
                        {
                            response.Status = "4 Bad Request";
                        }
                        if (request.Path != "" && requestPathId != "" && request.Method == "create")
                        {
                            response.Status = "4 Bad Request";
                        }
                        if (request.Path != "" && requestPathId == "" && request.Method == "create")
                        {
                            response.Status = "1 Ok";

                            var createRequest = request.Body.FromJson<Category>();
                            var newId = Program.categories.
                                OrderByDescending(x => x.Id)
                                .Select(x => x.Id)
                                .First();

                            Program.categories.Add(new Category { Id = newId +1, Name = createRequest.Name });

                            response.Status = "2 Created"; 
                            response.Body = Program.categories[newId].ToJson();
                        }

                        if (request.Path != "" && requestPathId == "" && (request.Method == "update" || request.Method == "delete"))
                        {
                            response.Status = "4 Bad Request";
                        }

                        if (request.Path != "" && requestPathId != "" && request.Method == "delete")
                        {
                            var delById = Program.categories.Find(x => x.Id == Convert.ToInt32(requestPathId));
                            
                            if (delById != null)
                            {
                                Program.categories.Remove(delById);
                                response.Status = "1 Ok";
                            }
                            else
                            {
                                response.Status = "5 not found";
                            }
                        }
                        if (request.Path == "/api/categories" && requestPathId == "" && request.Method == "read")
                        {
                            response.Status = "1 Ok";
                            response.Body = Program.categories.ToJson();
                        }
                        if (request.Path.Contains("/api/categories/") && requestPathId != "" && request.Method == "read")
                        {
                            response.Status = "1 Ok";

                            var readById = Program.categories.Find(x => x.Id == Convert.ToInt32(requestPathId));
                            response.Status = "1 Ok";
                            response.Body = readById.ToJson();
                        }

                        var allIds = Program.categories
                            .Select(x => x.Id);

                        if (request.Path.Contains("/api/categories/") && requestPathId != "" && (request.Method == "read" || request.Method == "update"))
                        {
                            if (!allIds.Contains(Convert.ToInt32(requestPathId))) 
                            {
                                response.Status = "5 not found";
                            }
                        }
                        
                        if (response.Status != "5 not found" && requestPathId != "" && request.Method == "update" && request.Body != null)
                        {
                            try { 
                                var updateReqest = request.Body.FromJson<Category>();
                                
                                if (allIds.Contains(updateReqest.Id))
                                {
                                    var tmp = Convert.ToInt32(updateReqest.Id);
                                    Program.categories[tmp-1].Name = updateReqest.Name;
                                    response.Status = "3 updated";
                                }
                            }
                            catch (System.Exception e)
                            {
                                Console.WriteLine("Exception: " + e, e);
                                response.Status = "4 Bad request, illegal body";
                            }
                        }
                    }
                    else
                    {
                        response.Status = "4 Bad Request";
                    }
                }

                // Testing cw's
                // Console.WriteLine("Request: " + request.ToJson());
                // Console.WriteLine("Response Status: " + response.Status);
                // Console.WriteLine("Response Body: " + response.Body);


                //*************************************
                //
                //write response to client.
                //print request and reponse in console.
                //
                //*************************************

                var serializedObj = JsonSerializer.Serialize<Response>(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var byteReplyMsg = Encoding.UTF8.GetBytes(serializedObj);
                stream.Write(byteReplyMsg, 0, byteReplyMsg.Length);
                Console.WriteLine("Thread_Id: {0} => Sends: {1}", Thread.CurrentThread.ManagedThreadId, response.ToJson());
                Console.WriteLine();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e, e);
                client.Close();
            }
        }

        private static string UnixTimestamp()
        {
            return DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
        }
    }
    public static class Util
    {
        public static Response ReadResponse(this TcpClient client)
        {
            var strm = client.GetStream();
           
            byte[] resp = new byte[2048];
            using (var memStream = new MemoryStream())
            {
                int bytesread = 0;
                do
                {
                    bytesread = strm.Read(resp, 0, resp.Length);
                    memStream.Write(resp, 0, bytesread);
                    var responseData2 = Encoding.UTF8.GetString(memStream.ToArray());
                } while (bytesread == 2048);

                var responseData = Encoding.UTF8.GetString(memStream.ToArray());
                return JsonSerializer.Deserialize<Response>(responseData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            }
        }
        public static Request ReadRequest(this TcpClient client)
        {
            var strm = client.GetStream();
            //strm.ReadTimeout = 250;
            byte[] resp = new byte[2048];
            using (var memStream = new MemoryStream())
            {
                int bytesread = 0;
                do
                {
                    bytesread = strm.Read(resp, 0, resp.Length);
                    memStream.Write(resp, 0, bytesread);
                    var responseData2 = Encoding.UTF8.GetString(memStream.ToArray());
                } while (bytesread == 2048);

                var requestData = Encoding.UTF8.GetString(memStream.ToArray());
                return JsonSerializer.Deserialize<Request>(requestData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            }
        }

        public static Boolean ArrayContains(string[] array, string stringToCheck)
        {
            if (stringToCheck == null)
            {
                return false;
            }
            foreach (string x in array)
            {
                if (stringToCheck.Contains(x))
                {
                    return true;
                }
            }
            return false;
        }
        public static string ToJson(this object data)
        {
            return JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
        public static T FromJson<T>(this string element)
        {
            return JsonSerializer.Deserialize<T>(element, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
    }
}