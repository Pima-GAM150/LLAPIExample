using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class Player
{
    public string playerName;
    public GameObject player;
    public int connectionId;
}

public class Client : MonoBehaviour {

    //How many people can connect to client/server
    private const int MAX_CONNECTION = 100;

    private int port = 5701;
    private string ip = "";

    private int hostId;
    private int webHostId;

    private int reliableChannel;
    private int unreliableChannel;

    private int ourClientId;
    private int connectionId;

    private float connectionTime;
    private bool isConnected = false;
    private bool isStarted = false;
    private byte error;

    private string playerName;

    public GameObject playerPrefab;
    public Dictionary<int, Player> players = new Dictionary<int, Player>();

    public void Connect()
    {
        //Make sure the player has a name
        playerName = GameObject.Find("NameInput").GetComponent<InputField>().text;
        if (playerName.Length == 0)
        {
            Debug.Log("Please enter a name!");
            return;
        }

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
        hostId = NetworkTransport.AddHost(topo, 0);
        //Connecting to the host
        connectionId = NetworkTransport.Connect(hostId, "127.0.0.1", port, 0, out error);

        connectionTime = Time.time;
        isConnected = true;
    }

    private void Update()
    {
        if(!isConnected)
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
        NetworkEventType recData = NetworkTransport.Receive(out recHostId, out connectionId, out channelId, recBuffer, bufferSize, out dataSize, out error);
        switch (recData)
        {
            case NetworkEventType.DataEvent:       //3
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                Debug.Log("Receiving: " + msg);
                string[] splitData = msg.Split('|');

                switch(splitData[0])
                {
                    case "ASKNAME":
                        OnAskName(splitData);
                        break;
                    case "CNN":
                        SpawnPlayer(splitData[1], int.Parse(splitData[2]));
                        break;
                    case "DC":
                        PlayerDisconnected(int.Parse(splitData[1]));
                        break;
                    case "ASKPOSITION":
                        OnAskPosition(splitData);
                        break;
                    default:
                        Debug.Log("Invalid message: " + msg);
                        break;
                }
                break;
        }
    }

    //ASKNAME|2|Jeff%1|temp%2
    private void OnAskName(string[] data)
    {
        //This client's ID
        ourClientId = int.Parse(data[1]);

        //Send this client's name to the server
        Send("NAMEIS|" + playerName, reliableChannel);

        //Create all of the other players
        //WHY IS DATA.LENGTH - 1 USED INSTEAD OF JUST DATA.LENGTH?
        for(int i = 2; i <data.Length - 1; i++)
        {
            string[] d = data[i].Split('%');
            SpawnPlayer(d[0], int.Parse(d[1]));
        }
    }

    private void OnAskPosition(string[] data)
    {
        if(!isStarted)
        {
            return;
        }
        //Update everyone else
        for(int i = 1; i < data.Length; i++)
        {
            string[] d = data[i].Split('%');

            if(int.Parse(d[0]) != ourClientId)
            {
                Vector3 position = Vector3.zero;
                position.x = float.Parse(d[1]);
                position.y = float.Parse(d[2]);
                players[int.Parse(d[0])].player.transform.position = position;
            }

        }

        //Send our position
        Vector3 myPosition = players[ourClientId].player.transform.position;
        string m = "MYPOSITION|" + myPosition.x.ToString() + "%" + myPosition.y.ToString();
        Send(m, unreliableChannel);
    }

    private void SpawnPlayer(string playerName, int cnnId)
    {
        GameObject newPlayer = (GameObject)Instantiate(playerPrefab);
        if(cnnId == ourClientId)
        {
            //Add movement
            //WHY DON'T YOU ADD THIS TO THE PREFAB ISNTEAD OF HERE?
            newPlayer.AddComponent<PlayerMovement>();
            //Remove Canvas
            GameObject.Find("Canvas").SetActive(false);
            isStarted = true;
        }

        Player p = new Player();
        p.player = newPlayer;
        p.playerName = playerName;
        p.player.GetComponentInChildren<TextMesh>().text = playerName;
        p.connectionId = cnnId;

        players.Add(p.connectionId, p);
    }

    private void PlayerDisconnected(int cnnId)
    {
        Destroy(players[cnnId].player);
        players.Remove(cnnId);
    }

    private void Send(string message, int channelId)
    {
        Debug.Log("Sending: " + message);
        byte[] msg = Encoding.Unicode.GetBytes(message);

        NetworkTransport.Send(hostId, connectionId, channelId, msg, message.Length * sizeof(char), out error);

    }
}
