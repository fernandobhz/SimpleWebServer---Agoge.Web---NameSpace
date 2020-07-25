using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace SimpleWebServer___Agoge.Web___NameSpace {
    static class Program {
        [STAThread]
        static void Main() {
            var port = 555;

            Console.WriteLine("Accepting connections on port " + port);

            Agoge.Web.SimpleWebServer.register(new Agoge.Web.ResourceHandler {
                resource = "/hora",
                handler = (byte[] req) => {
                    return new DateTime().ToString();
                }
            });

            Agoge.Web.SimpleWebServer.register(new Agoge.Web.ResourceHandler {
                resource = "/pi",
                handler = (byte[] req) => {
                    var res = System.Text.Encoding.UTF8.GetBytes("3.14159265");
                    return res;
                }
            });

            Agoge.Web.SimpleWebServer.start(port);

            Application.Run();
        }
    }

}

namespace Agoge {

    namespace Web {

        public class ResourceHandler {
            public string resource { get; set; }
            public Func<byte[], Object> handler { get; set; }
        }

        public class SimpleWebServer {

            private static List<TcpListener> masterTcpListeners = new List<TcpListener>();

            private static TcpClient wait(int port) {
                var existing = masterTcpListeners.SingleOrDefault(x => ((IPEndPoint)x.LocalEndpoint).Port == port);

                if (existing == null) {
                    masterTcpListeners.Add(new TcpListener(System.Net.IPAddress.Any, port));
                    existing = masterTcpListeners.Last();
                    existing.Start();
                }

                return existing.AcceptTcpClient();
            }

            private static List<ResourceHandler> resources = new List<ResourceHandler>();

            public static void register(ResourceHandler rh) {
                var existing = resources.SingleOrDefault(x => x.resource == rh.resource);

                if (existing == null) {
                    resources.Add(rh);
                    existing = rh;
                }
            }

            public static void start(int port) {
                do {
                    var tcpClient = SimpleWebServer.wait(port);

                    Task.Factory.StartNew(
                        () => {
                            var networkStream = tcpClient.GetStream();
                            var requestBuff = new byte[4096];
                            var requestResource = "";

                            try {
                                networkStream.Read(requestBuff, 0, 4096);
                            } catch { return; }


                            try {
                                var requestString = System.Text.Encoding.UTF8.GetString(requestBuff);
                                var requestLines = requestString.Replace("\r", "").Split((char)10);
                                var requestLine = (String)requestLines.First();
                                var requestParams = requestLine.Split(" "[0]);

                                requestResource = requestParams[1];
                            } catch { return; }

                            var existing = resources.SingleOrDefault(x => x.resource == requestResource);

                            if (existing == null) {
                                Task.Factory.StartNew(() => {
                                    var responseBuffer = System.Text.Encoding.UTF8.GetBytes("HTTP/1.0 404 Not Found\r\n\r\n");
                                    networkStream.Write(responseBuffer, 0, responseBuffer.Length);
                                    tcpClient.Close();
                                    return;
                                });
                            } else {
                                Task.Factory.StartNew(() => {
                                    var responseBytes = new List<byte>();
                                    responseBytes.AddRange(System.Text.Encoding.UTF8.GetBytes("HTTP/1.0 200 OK\r\n\r\n").ToArray());


                                    var responseObject = existing.handler.Invoke(requestBuff);

                                    byte[] contentBuffer;

                                    if (responseObject.GetType() == typeof(String))
                                        contentBuffer = System.Text.Encoding.UTF8.GetBytes((String)responseObject);
                                    else
                                        contentBuffer = (Byte[])responseObject;

                                    responseBytes.AddRange(contentBuffer);
                                    var responseBuffer = responseBytes.ToArray();


                                    networkStream.Write(responseBuffer, 0, responseBuffer.Length);
                                    tcpClient.Close();
                                    return;
                                });
                            }
                        });

                } while (true);
            }
        }
    }
}
