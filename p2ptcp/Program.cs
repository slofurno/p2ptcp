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
    static HashSet<IPAddress> knownips = new HashSet<IPAddress>();
    static IPAddress myipaddress;
    static int DEFAULT_PORT = 1278;
    static string name = "";
    static char MSG_CODE = (char)215;
    static char USER_CODE = (char)216;
    static char WELCOME_CODE = (char)217;

    static void Main(string[] args)
    {

      Console.WriteLine(args[0]);
      
      //args : name iptoconnectto
      var tasks = new List<Task>();

      name = args[0];
      //var port = int.Parse(args[1]);
      tasks.Add(StartListening(DEFAULT_PORT));

      if (args.Length > 1)
      {
        Console.WriteLine(args[1]);
        var ip = IPAddress.Parse(args[1]);
        //var theirport = int.Parse(args[3]);
        tasks.Add(connect(ip, DEFAULT_PORT));
      }

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
        var remoteip = endpoint.Address;

        if (knownips.Add(remoteip))
        {
          connections.Add(client);
          connectionlisteners.Add(handleConnection(client));
        }

        

      }

      await Task.WhenAll(connectionlisteners);

    }

    static async Task connect(IPAddress ip, int port)
    {

      TcpClient client = new TcpClient();
      try
      {
        await client.ConnectAsync(ip, port);
        if (knownips.Add(ip))
        {
          connections.Add(client);
          connectionlisteners.Add(handleConnection(client));
        }
      }
      catch (Exception e)
      {
        Console.WriteLine("failed to connect with error " + e.Message);
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
    }

    static async Task handleConnection(TcpClient client)
    {
      
      var endpoint = client.Client.RemoteEndPoint as IPEndPoint;
      var remoteip = endpoint.Address;
      Console.WriteLine("client connected : " + remoteip.ToString());

      sendmessage(client, WELCOME_CODE + remoteip.ToString());

      var rec = new List<string>();
      var network = client.GetStream();
      var buffer = new byte[4096];
      int len;
     
      while((len = await network.ReadAsync(buffer, 0, 4096))>0)
      {
        var content = System.Text.Encoding.UTF8.GetString(buffer,0,len);
        rec.Add(content);
        var code = content[0];
        var body = content.Substring(1).Trim();

        if (code == MSG_CODE)
        {
          Console.WriteLine(body);
        }
        else if (code == USER_CODE)
        {
          var ip = IPAddress.Parse(body);
          //var match2 = connections.Where(x => ((IPEndPoint)(x.Client.RemoteEndPoint)).Address == ip).FirstOrDefault();
          if (ip != myipaddress)
          {
            Console.WriteLine("connecting to " + body);
            connect(ip, DEFAULT_PORT);
          }
        }
        else if (code == WELCOME_CODE && myipaddress==null)
        {
          Console.WriteLine("my ip address is : " + body);
          myipaddress = IPAddress.Parse(body);
        }
        
      }

      Console.WriteLine("client disconnected : " + remoteip.ToString());
      connections.Remove(client);
      knownips.Remove(remoteip);


    }
  }
}
