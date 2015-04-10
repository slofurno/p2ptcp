using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace p2ptcp
{
  class Program
  {

    static List<TcpClient> connections = new List<TcpClient>();
    static List<Task> connectionlisteners = new List<Task>();
    //static HashSet<IPEndPoint> knownendpoints = new HashSet<IPEndPoint>();
    //static IPEndPoint myipaddress;

    static HashSet<string>remoteips = new HashSet<string>();
    static int DEFAULT_PORT = 1278;
    static string name = "";
    static char MSG_CODE = (char)215;
    static char USER_CODE = (char)216;
    static char WELCOME_CODE = (char)217;
    static char PORT_CODE = (char)218;
    static int mPort = 0;
    static string mIpAddress;

    static void Main(string[] args)
    {

      Console.WriteLine(args[0]);
      
      //args : name iptoconnectto
      var tasks = new List<Task>();

      name = args[0];

      var myport = DEFAULT_PORT;//int.Parse(args[1]);
      mPort = myport;
      tasks.Add(StartListening(myport));

      if (args.Length > 1)
      {
        var ipendpoint = args[1];

        Task.Delay(100).Wait();

        var ip = IPAddress.Parse(ipendpoint);
        //var theirport = DEFAULT_PORT;//int.Parse(split[1]);
        //var endpoint = new IPEndPoint(ip, theirport);

        tasks.Add(connect(ip));
      }
      /*
      else if (args.Length > 1)
      {
        Console.WriteLine(args[1]);
        var ip = IPAddress.Parse(args[1]);
        tasks.Add(connect(ip, DEFAULT_PORT));
      }
      */
      Task.WhenAll(tasks).Wait();
    }

    static async Task StartListening(int port)
    {

      //IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
      TcpListener listener = new TcpListener(IPAddress.Any, port);
      var input = readconsoleinput();

      listener.Start();

      while (true) 
      {

        var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);

        var endpoint = client.Client.RemoteEndPoint as IPEndPoint;
        //knownendpoints.Add(endpoint);
        //var remoteip = endpoint.Address;
        
        connectionlisteners.Add(handleConnection(client));
  
      }

      await Task.WhenAll(connectionlisteners);

    }

    static async Task connect(IPAddress ip)
    {
      
      try
      {
        if (remoteips.Add(ip.ToString()))
        {
          TcpClient client = new TcpClient();
          await client.ConnectAsync(ip, DEFAULT_PORT);
          //connections.Add(client);
          connectionlisteners.Add(handleConnection(client));
        }
      }
      catch (Exception e)
      {
        Console.WriteLine("failed to connect with error " + e.Message);
        remoteips.Remove(ip.ToString());
      }

    }

    static async Task readconsoleinput()
    {
      using (var input = Console.OpenStandardInput())
      using (var reader = new StreamReader(input))
      {
        while (true)
        {
          var next = await reader.ReadLineAsync();
          var chars = next.ToCharArray();
          broadcast(MSG_CODE + name + ": " + next);
        }
      }


    }

    static async Task broadcast(string line)
    {
      foreach (var client in connections)
      {
        await sendmessage(client, line);
      }
    }

    static async Task sendmessage(TcpClient client, string line)
    {
      var stream = client.GetStream();
      var writer = new StreamWriter(stream);
      writer.AutoFlush = true;
      await writer.WriteLineAsync(line);
      writer.Flush();

    }

    static async Task handleConnection(TcpClient client)
    {

      int remotelistenport = 0;
      var endpoint = client.Client.RemoteEndPoint as IPEndPoint;
      var remoteipaddress = endpoint.Address.ToString();
      var stringpoint = endpoint.ToString();

      Console.WriteLine("client connected : " + stringpoint);

      sendmessage(client, WELCOME_CODE + remoteipaddress);


      
      var rec = new List<string>();
      var buffer = new byte[4096];
      int len;

      var stream = client.GetStream();
      try
      {
        var reader = new StreamReader(stream);
        while(true)
       // while ((len = await stream.ReadAsync(buffer, 0, 4096)) > 0)
        {
          //var content = System.Text.Encoding.UTF8.GetString(buffer, 0, len);
          var content = await reader.ReadLineAsync();
          var chars = content.ToCharArray();


          rec.Add(content);
          var code = content[0];
          var body = content.Substring(1).Trim();

          if (!client.Connected)
          {
            Console.WriteLine("disconnected..");
            break;
          }

          if (code == MSG_CODE)
          {
            Console.WriteLine(body);
          }
          else if (code == USER_CODE)
          {
            var split = body.Split(':');
            //var ip = IPAddress.Parse(body);
            if (remoteips.Add(split[0])){
              
              var ip = IPAddress.Parse(split[0]);
              var theirport = DEFAULT_PORT;
              var endpt = new IPEndPoint(ip, theirport);
              connect(endpoint.Address);
              //var match2 = connections.Where(x => ((IPEndPoint)(x.Client.RemoteEndPoint)).Address == ip).FirstOrDefault();
              /*
              if (endpt.Address != myipaddress.Address && endpt.Port != myipaddress.Port)
              {
                Console.WriteLine("learned about: " + body);
                connect(endpt);
              }
               * */
            }
          }
          else if (code == WELCOME_CODE)
          {
            sendmessage(client, PORT_CODE + mPort.ToString());
          }
          else if (code == PORT_CODE)
          {
            remotelistenport = int.Parse(body);
            string remoteendpoint = endpoint.Address.ToString();// + ":" + DEFAULT_PORT;
            remoteips.Add(remoteendpoint);
            broadcast(USER_CODE + remoteendpoint);
            connections.Add(client);
            
          }

          if (code == WELCOME_CODE && mIpAddress == null)
          {
            Console.WriteLine("my ip address is : " + body);
            mIpAddress = body;
          }

        }

      }
      catch (Exception e)
      {

      }
      finally
      {
        stream.Dispose();
        Console.WriteLine("client disconnected : " + endpoint.ToString());
        connections.Remove(client);
        remoteips.Remove(remoteipaddress);
      }


     


    }
  }

  public class P2pConnection
  {
    public TcpClient client { get; set; }
    public Task listener { get; set; }
    public int port { get; set; }

    public P2pConnection()
    {

    }
  }

}
