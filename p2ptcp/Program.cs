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

    static HashSet<string>remoteips = new HashSet<string>();
    static int DEFAULT_PORT = 1278;
    static string name = "";
    static char MSG_CODE = (char)215;
    static char USER_CODE = (char)216;
    static char WELCOME_CODE = (char)217;
    static int mPort = 0;
    static string mIpAddress;

    static void Main(string[] args)
    {

      Console.WriteLine(args[0]);
      
      var startupTasks = new List<Task>();

      name = args[0];

      mPort = DEFAULT_PORT;
      startupTasks.Add(StartListening(DEFAULT_PORT));

      if (args.Length > 1)
      {
        var ipendpoint = args[1];
        var ip = IPAddress.Parse(ipendpoint);
        startupTasks.Add(connect(ip));
      }

      Task.WhenAll(startupTasks).Wait();
    }

    static async Task StartListening(int port)
    {
      //listen on whatever ip address you can, on our default port.
      TcpListener listener = new TcpListener(IPAddress.Any, port);
      
      //a task which we don't await, which sits in the background and waits for your inputs. 
      var input = readconsoleinput();

      listener.Start();

      while (true) 
      {
        var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);

        var endpoint = client.Client.RemoteEndPoint as IPEndPoint;
        var remoteip = endpoint.Address;

        //if you are already connected to this ip, don't connect again. 
        if (remoteips.Add(remoteip.ToString()))
        {
          connectionlisteners.Add(handleConnection(client));
        }
        else
        {
          client.Close();
        }
  
      }
      //you will never get here, but if you did, it would wait for all your connections to close.
      await Task.WhenAll(connectionlisteners);
    }

    static async Task connect(IPAddress ip)
    {
      try
      {
        //if we arent already connected to this user, connect
        if (remoteips.Add(ip.ToString()))
        {
          TcpClient client = new TcpClient();
          client.SendTimeout = 1000;
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
      //interpret the console input stream as a text stream
      using (var reader = new StreamReader(input))
      {
        while (true)
        {
          //yield control until you enter another line of input
          var next = await reader.ReadLineAsync();
          //fire and forget this broadcast task
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

    //every message will have a code appended to the beginning of it, so the receiever knows what kind of message it is.
    static async Task sendmessage(TcpClient client, string line)
    {
      //cant wrap this in using block, cause disposing of the underlying stream would end our connection
      var stream = client.GetStream();
      var writer = new StreamWriter(stream);
      writer.AutoFlush = true;
      await writer.WriteLineAsync(line);
      //prolly dont need this and autoflush
      writer.Flush();

    }

    static async Task handleConnection(TcpClient client)
    {

      var endpoint = client.Client.RemoteEndPoint as IPEndPoint;
      var remoteipaddress = endpoint.Address.ToString();

      Console.WriteLine("client connected : " + remoteipaddress);

      //there is no way to know your own ip address, so we kindly welcome each user who connects and tell them what their ip address looks like to us.
      await sendmessage(client, WELCOME_CODE + remoteipaddress);

      //we then share their ip address with everyone we're already connected to, so they can also connect to them
      await broadcast(USER_CODE + remoteipaddress);
      connections.Add(client);
      var stream = client.GetStream();

      //typically you know end of stream when you read a byte length of 0, but this didn't seem to be the case on ubuntu, so i found it more consistent to do it like this.
      try
      {
        var reader = new StreamReader(stream);
        while(true)
        {
          var content = await reader.ReadLineAsync();
          var chars = content.ToCharArray();

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
          //user code means someone is sharing another user's connection info with us. if we don't have them, we will connect
          else if (code == USER_CODE)
          {
            if (mIpAddress!=body){
              Console.WriteLine("learned about user at ip : " + body);
              var ip = IPAddress.Parse(body);
              await connect(ip);
            }
          }
          //after you connect to someone, they respond with a welcome message containing your ip address
          else if (code == WELCOME_CODE && mIpAddress == null)
          {
            Console.WriteLine("my ip address is : " + body);
            mIpAddress = body;
          }
        }

      }
      catch (Exception e)
      {
        //this might not be the best way of handling this, but its the only way i could get it to work on windows + linux
      }
      finally
      {
        //when our while loop is broken, we come here and dispose our connection, and do some bookkeeping
        stream.Dispose();
        Console.WriteLine("client disconnected : " + endpoint.ToString());
        connections.Remove(client);
        remoteips.Remove(remoteipaddress);
      }

    }
  }


}
