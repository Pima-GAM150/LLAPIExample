using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ServerClient
{
    public int connectionId;
    public string playerName;
    public Vector3 playerPosition;
}

public class Server : MonoBehaviour {

    //How many people can connect to client/server
    private const int MAX_CONNECTION = 100;

    private int port = 5701;
    //Do not need this for a server
    //private string ip = "";

    private int hostId;
    //private int webHostId;

    private int reliableChannel;
    private int unreliableChannel;

    private bool isStarted = false;
    private byte error;

    private List<ServerClient> clients = new List<ServerClient>();

    private float lastMovementUpdate;
    private float movementUpdateRate = 0.5f;

    private void Start()
    {
        NetworkTransport.Init();
        ConnectionConfig cc = new ConnectionConfig();

        //https://blogs.unity3d.com/2014/06/11/all-about-the-unity-networking-transport-layer/
        reliableChannel = cc.AddChannel(QosType.Reliable);

        unreliableChannel = cc.AddChannel(QosType.Unreliable);
        //Used for very large messages
        //QosType.UnreliableFragmented
        //Used for things like video and voice, when it is imporant that the large message arrives in a specific order
        //QosType.UnreliableFragmentedSequenced

        HostTopology topo = new HostTopology(cc, MAX_CONNECTION);
        //Parameters are topo, port, ip (the ip address is who you will accept connections from, null = everyone)
        hostId = NetworkTransport.AddHost(topo, port, null);

        isStarted = true;
    }

    //
    private void Update()
    {
        if(!isStarted)
        {
            return;
        }

        int recHostId;
        int connectionId;
        int channelId;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error;
        /*
         *recHostId - host ID
         * connectionId - which client this is from
         * channelId - which channel is being used (reliable/unreliable)
         * recBuffer - this is the message
         * bufferSize - 1024 (if your message is larger than the bufferSize you have to fragment it)
         * dataSize - how large the bufferSize is (lets you know when to stop reading from the recBuffer so you don't get run past it in memory)
         */
        NetworkEventType recData = NetworkTransport.Receive(out recHostId, out connectionId, out channelId, recBuffer, bufferSize, out dataSize, out error);
        switch (recData)
        {
            case NetworkEventType.Nothing:         //1
                break;
            case NetworkEventType.ConnectEvent:    //2
                Debug.Log("Player " + connectionId + " has connected!");
                OnConnection(connectionId);
                break;
            case NetworkEventType.DataEvent:       //3 - this is where the magic happens
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                Debug.Log("Player " + connectionId + " has sent: " + msg);
                string[] splitData = msg.Split('|');

                switch (splitData[0])
                {
                    case "NAMEIS":
                        OnNameIs(connectionId, splitData[1]);
                        break;
                    case "MYPOSITION":
                        OnMyPosition(connectionId, splitData[1]);
                        break;
                    default:
                        Debug.Log("Invalid message: " + msg);
                        break;
                }
                break;
            case NetworkEventType.DisconnectEvent: //4
                Debug.Log("Player " + connectionId + " has disconnected!");
                OnDisconnection(connectionId);
                break;
        }

        //Ask players for their position
        lastMovementUpdate += Time.deltaTime;
        if(lastMovementUpdate > movementUpdateRate)
        {
            lastMovementUpdate = 0f;
            string msg = "ASKPOSITION|";
            foreach(ServerClient sc in clients)
            {
                msg += sc.connectionId.ToString() + "%" + sc.playerPosition.x.ToString() + "%" + sc.playerPosition.y.ToString() + "|";
            }
            msg = msg.Trim('|');
            Send(msg, unreliableChannel, clients);
        }
    }

    private void OnConnection(int cnnId)
    {
        //Add to a players list
        ServerClient c = new ServerClient();
        c.connectionId = cnnId;
        c.playerName = "temp";
        clients.Add(c);

        //When player join server, send player id, and ask for name
        string msg = "ASKNAME|" + cnnId + "|";
        foreach(ServerClient sc in clients)
        {
            //IT IS IMPORTANT TO KNOW THE DIFFERENT BETWEEN "" AND ''
            //try change "%" to '%' and see what happens
            msg += sc.playerName + "%" + sc.connectionId + "|";
        }

        msg = msg.Trim('|');
        //ASKNAME|2|Jeff%1|temp%2

        //send name/id of all connected player(s)
        Send(msg, reliableChannel, cnnId);
        
    }

    private void OnDisconnection(int cnnId)
    {
        clients.Remove(clients.Find(x => x.connectionId == cnnId));
        Send("DC|" + cnnId, reliableChannel, clients);
    }

    private void OnNameIs(int cnnId, string playerName)
    {
        //Change name from TEMP
        clients.Find(x => x.connectionId == cnnId).playerName = playerName;

        //Let all players know someone has connected
        Send("CNN|" + playerName + "|" + cnnId, reliableChannel, clients);
    }

    private void OnMyPosition(int cnnId, string pos)
    {
        string[] newPosition = pos.Split('%');
        clients.Find(c => c.connectionId == cnnId).playerPosition = new Vector3(float.Parse(newPosition[0]), float.Parse(newPosition[1]), 0f);
    }

    //Send a message to a specific player
    private void Send(string message, int channelId, int cnnId)
    {
        List<ServerClient> c = new List<ServerClient>();
        c.Add(clients.Find(x => x.connectionId == cnnId));
        Send(message, channelId, c);
    }

    private void Send(string message, int channelId, List<ServerClient> c)
    {
        Debug.Log("Sending: " + message);
        byte[] msg = Encoding.Unicode.GetBytes(message);
        foreach(ServerClient sc in c)
        {
            NetworkTransport.Send(hostId, sc.connectionId, channelId, msg, message.Length * sizeof(char), out error);
        }
    }

}
