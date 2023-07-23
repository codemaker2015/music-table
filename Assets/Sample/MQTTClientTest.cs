using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using SimpleJSON;
using UnityEngine;
using UnityEngine.UI;

public class MQTTClientTest : MonoBehaviour {
    private string ipAddress = "192.168.0.106";
    private int port = 1883;
    private string ipMessage = "";
    private string subTopic = "reactive";
    private string pubTopic = "reactive";
    private string txtMessage = "";
    private int[] positionMapper = { 1, 3, 5, 7, 9, 10, 8, 6, 4, 2, 15, 13, 12, 14, 11 };

    string payload;
    public Text message;

    IMqttClient client;
    StringBuilder sb = new StringBuilder ();

    async void Start () {
        client = new MqttFactory ().CreateMqttClient ();
        client.Connected += OnConnected;
        client.Disconnected += OnDisconnected;
        client.ApplicationMessageReceived += OnApplicationMessageReceived;

        await ConnectAsync (ipAddress);
    }

    async void OnDestroy () {
        client.Connected -= OnConnected;
        client.Disconnected -= OnDisconnected;
        client.ApplicationMessageReceived -= OnApplicationMessageReceived;

        Debug.Log ("start disconnect");
        await client.DisconnectAsync ();
        Debug.Log ("disconnected");
    }

    public async void Connect () {
        await ConnectAsync (ipAddress);
    }

    public async Task ConnectAsync (string address) {
        var options = new MqttClientOptionsBuilder ().WithTcpServer (address, port).Build ();

        var result = await client.ConnectAsync (options);
        Debug.Log ($"Connected to the broker: {result.IsSessionPresent}");

        var topic = new TopicFilterBuilder ().WithTopic (subTopic).Build ();
        await client.SubscribeAsync (subTopic);

        Debug.Log ("Subscribed");
    }

    public async void PublishMessage () {
        await PublishMessageAsync (ipMessage);
    }

    public async Task PublishMessageAsync (string ipMessage) {
        var msg = new MqttApplicationMessageBuilder ()
            .WithTopic (pubTopic)
            .WithPayload (ipMessage)
            .WithExactlyOnceQoS ()
            .Build ();
        await client.PublishAsync (msg);
    }

    private void OnConnected (object sender, MqttClientConnectedEventArgs e) {
        Debug.Log ($"On Connected: {e}");
    }

    private void OnDisconnected (object sender, MqttClientDisconnectedEventArgs e) {
        Debug.Log ($"On Disconnected: {e}");
    }

    private void OnApplicationMessageReceived (
        object sender,
        MqttApplicationMessageReceivedEventArgs e
    ) {
        //sb.Clear();
        //sb.AppendLine("Message:");
        //sb.AppendFormat("ClientID: {0}\n", e.ClientId);
        //sb.AppendFormat("Topic: {0}\n", e.ApplicationMessage.Topic);
        //sb.AppendFormat("Payload: {0}\n", Encoding.UTF8.GetString(e.ApplicationMessage.Payload));
        //sb.AppendFormat("QoS: {0}\n", e.ApplicationMessage.QualityOfServiceLevel);
        //sb.AppendFormat("Retain: {0}\n", e.ApplicationMessage.Retain);

        //Debug.Log(sb);

        // JSONNode jsonNode = JSON.Parse(Encoding.UTF8.GetString(e.ApplicationMessage.Payload));
        // string value = jsonNode["command"].ToString().Trim('"');

        payload = Encoding.UTF8.GetString (e.ApplicationMessage.Payload);
        string[] datas = payload.Split (' ');
        // Have to rearrange the array / pins based on the value
        for (int i = 0; i < datas.Length; i++)
            Global.datas[i] = getValue (datas, positionMapper[i]);

    }
    // Have to rearrange the array / pins based on the value
    private int getValue (string[] datas, int pos) {
        for (int i = 0; i < datas.Length; i++)
            if (int.Parse (datas[i].Split ('-') [0]) == pos)
                return datas[i].Split ('-') [1] == "0" ? 1 : 0;

        return -1;
    }

    private void FixedUpdate () {
        for (int i = 0; i < Global.datas.Length; i++)
            if (Global.datas[i] == 1)
                message.text = i + " : " + Global.datas[i];
    }

    async void SendWebSocketMessage (int code) {
        await PublishMessageAsync ("{\"command\": \"" + code + "\"}");
    }

    private async void OnApplicationQuit () {
        // close connection
    }
}