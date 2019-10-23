using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Json;
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

        public static readonly string[] methods = { Read, Create, Update, Delete };
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
        public override string ToString()
        {
            return "Method: " + Method + ", Path: " + Path + ", Date: " + Date + ", Body: " + Body;
        }
    }

    public class Category
    {
        [JsonPropertyName("cid")]
        public int Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        public override string ToString()
        {
            return "Id: " + Id + ", Name: " + Name;
        }
    }

    class Program
    {

        public static List<Category> categories = new List<Category>
    {
      new Category {Id = 1, Name = "Beverages"},
      new Category{Id = 2, Name = "Condiments"},
      new Category{Id = 3, Name = "Confections"}
    };

        public static readonly string[] methods = { };

        static void Main(string[] args)
        {
            new Server(IPAddress.Parse("127.0.0.1"), 5000);
            Console.WriteLine("Server Started");
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
                    Console.WriteLine("Waiting for a connection...");
                    var client = server.AcceptTcpClient();

                    Console.WriteLine($"A client connected!");
                    Thread t = new Thread(new ParameterizedThreadStart(HandleDevice));
                    t.Start(client);

                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
                server.Stop();
            }
        }

        public void HandleDevice(object obj)
        {
            TcpClient client = (TcpClient)obj;
            var stream = client.GetStream();
            Response response = new Response();

            var buffer = new byte[client.ReceiveBufferSize];
            int receiveBufferCount;
            try
            {
                // while ((receiveBufferCount = stream.Read(buffer, 0, buffer.Length)) != 0)
                // {
                // var msg = Encoding.UTF8.GetString(buffer, 0, buffer.Length);
                // Console.WriteLine(msg);
                var request = client.ReadRequest();
                if (request.Method == null)
                {
                    response.Status = "missing method";
                    // goto Finish;
                }
                if (request.Path == null)
                {
                    response.Status += "missing resource";
                }
                if (request.Body == null)
                {
                    response.Status += ", missing body";
                }
                if (request.Date == null)
                {
                    response.Status += ", missing date";
                    goto Finish;
                }

                if (!(Util.ArrayContains(Method.methods, request.Method)))
                {
                    response.Status += "illegal method";
                    goto Finish;
                }
                // check if date in correct format
                if (request.Date != null)
                {
                    try
                    {
                        var number = Convert.ToInt32(request.Date);
                    }
                    catch (System.Exception)
                    {

                        response.Status += ", illegal date";
                    }

                    goto Finish;
                }

            Finish:
                //invalid json object in body
                if (request.Body != null)
                {
                    try
                    {
                        var categoryFromJson = JsonSerializer.Deserialize<Request>(request.Body);
                    }
                    catch (System.Exception e)
                    {
                        Console.WriteLine(e);

                        response.Status += ", illegal body";
                    }
                    response.Body = "Hello World";
                    goto End;
                }

            End:
                if (request.Path != null && request.Path.Contains("/api"))
                {
                    if (request.Path.Contains("/api/categories"))
                    {
                        var extractedId = Regex.Match(request.Path, @"\d+$").Value;

                        if (extractedId == "")
                        {
                            response.Status = "4 Bad Request";
                        }
                        if (request.Path != "" && extractedId != "" && request.Method == "create")
                        {
                            response.Status = "4 Bad Request";
                            response.Body = null;
                        }
                        if (request.Path != "" && extractedId == "" && request.Method == "create")
                        {
                            response.Status = "1 Ok";
                            var category = request.Body.FromJson<Category>();

                            // Console.WriteLine(category);
                            // Console.WriteLine(123);
                            Program.categories.Add(new Category { Id = 4, Name = category.Name });

                            var stringCategory = Program.categories[3].ToJson();
                            Console.WriteLine(stringCategory);
                            response.Body = stringCategory;
                            // foreach (var item in Program.categories)
                            // {
                            //   Console.WriteLine(item);
                            // }
                        }
                        if (request.Path != "" && extractedId == "" && request.Method == "update")
                        {
                            response.Status = "4 Bad Request";
                            response.Body = null;
                        }
                        if (request.Path != "" && extractedId == "" && request.Method == "delete")
                        {
                            response.Status = "4 Bad Request";
                            response.Body = null;
                        }
                        if (request.Path != "" && extractedId != "" && request.Method == "delete")
                        {
                            var item = Program.categories.Find(x => x.Id == Int32.Parse(extractedId));
                            if (item != null)
                            {
                                Program.categories.Remove(item);
                                response.Status = "1 Ok";
                            }
                            else
                            {
                                response.Status = "5 not found";
                            }
                        }
                        if (request.Path == "/api/categories" && extractedId == "" && request.Method == "read")
                        {
                            response.Status = "1 Ok";
                            var categories = new List<object>
            {
                new {cid = 1, name = "Beverages"},
                new {cid = 2, name = "Condiments"},
                new {cid = 3, name = "Confections"}
            };
                            response.Body = categories.ToJson();
                        }
                        if (request.Path == "/api/categories/1" && extractedId == "1" && request.Method == "read")
                        {
                            response.Status = "1 Ok";
                            // response.Body = (new { cid = 1, name = "Beverages" }.ToJson());
                            foreach (var item in Program.categories)
                            {
                                if (item.Id == 1)
                                {
                                    response.Body = (new { cid = 1, name = item.Name }.ToJson());
                                }
                            }
                        }
                        if (request.Path == "/api/categories/123" && extractedId != "" && request.Method == "read")
                        {
                            response.Status = "5 not found";
                        }
                        if (request.Path == "/api/categories/123" && extractedId != "" && request.Method == "update")
                        {
                            response.Status = "5 not found";
                        }
                        if (request.Path == "/api/categories/1" && extractedId != "" && request.Method == "update" && request.Body != null)
                        {

                            Console.WriteLine(request.Body);
                            try
                            {
                                var category = request.Body.FromJson<Category>();
                                foreach (var item in Program.categories)
                                {
                                    if (item.Id == category.Id)
                                    {
                                        item.Name = category.Name;
                                    }
                                }

                                response.Status = "3 updated";
                                // foreach (var item in Program.categories)
                                // {
                                //   Console.WriteLine(item);
                                // }
                            }
                            catch (System.Exception e)
                            {

                            }

                        }
                    }
                    else
                    {
                        response.Status = "4 Bad Request";
                    }
                }

                Console.WriteLine(request.ToString());
                Console.WriteLine("Response Status: ", response.Status);
                Console.WriteLine("Response Body: ", response.Status);
                // Console.WriteLine(response.Status);
                // foreach (var item in Program.categories)
                // {
                //  Console.WriteLine(item);   
                // }

                // implement some kind of cleanup if client sends close message(server sid)
                // if (msg == "exit2") client.Close();
                // Console.WriteLine("{1}: Received: {0}", msg, Thread.CurrentThread.ManagedThreadId);

                // var serializeObject = new DataContractJsonSerializer(typeof(Response));

                var serializedObj = JsonSerializer.Serialize<Response>(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var byteReplyMsg = Encoding.UTF8.GetBytes(serializedObj);
                stream.Write(byteReplyMsg, 0, byteReplyMsg.Length);
                Console.WriteLine("{1}: Sent: {0}", response, Thread.CurrentThread.ManagedThreadId);
                // }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: {0}", e.ToString());
                client.Close();
            }
        }
    }
    public static class Util
    {
        public static Response ReadResponse(this TcpClient client)
        {
            var strm = client.GetStream();
            // Console.WriteLine(strm);
            //strm.ReadTimeout = 250;
            byte[] resp = new byte[2048];
            using (var memStream = new MemoryStream())
            {
                int bytesread = 0;
                do
                {
                    bytesread = strm.Read(resp, 0, resp.Length);
                    // Console.WriteLine(bytesread);
                    // Console.WriteLine("@@@@@@@");
                    // Console.WriteLine(bytesread);
                    // Console.WriteLine("######");
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
                    // Console.WriteLine(bytesread);
                    // Console.WriteLine("@@@@@@@");
                    // Console.WriteLine(bytesread);
                    // Console.WriteLine("######");
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
        public static DateTime UnixTimeStampToDateTime(int unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }

    }
}