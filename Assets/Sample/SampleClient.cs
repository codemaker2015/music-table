using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using MQTTnet;
using MQTTnet.Client;

public class SampleClient : MonoBehaviour
{
    [SerializeField]
    InputField ipAddress;
    [SerializeField]
    InputField port;
    [SerializeField]
    InputField ipMessage;
    [SerializeField]
    InputField ipTopic;
    [SerializeField]
    Text txtMessage;
    private string msgText = "";

    IMqttClient client;
    StringBuilder sb = new StringBuilder();

    async void Start()
    {
        client = new MqttFactory().CreateMqttClient();
        client.Connected += OnConnected;
        client.Disconnected += OnDisconnected;
        client.ApplicationMessageReceived += OnApplicationMessageReceived;

        // await ConnectAsync(ipAddress.text);
        txtMessage.text = "";
    }

    async void OnDestroy()
    {
        client.Connected -= OnConnected;
        client.Disconnected -= OnDisconnected;
        client.ApplicationMessageReceived -= OnApplicationMessageReceived;

        Debug.Log("start disconnect");
        await client.DisconnectAsync();
        Debug.Log("disconnected");
    }

    void Update()
    {
        txtMessage.text = msgText;
    }

    public async void Connect()
    {
        await ConnectAsync(ipAddress.text);
    }
    
    public async Task ConnectAsync(string address)
    {
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(address, int.Parse(port.text))
            .Build();

        var result = await client.ConnectAsync(options);
        Debug.Log($"Connected to the broker: {result.IsSessionPresent}");

        var topic = new TopicFilterBuilder()
            .WithTopic(ipTopic.text)
            .Build();
        await client.SubscribeAsync("/" + ipTopic.text);

        Debug.Log("Subscribed");
    }

    public async void PublishMessage()
    {
        await PublishMessageAsync();
    }

    public async Task PublishMessageAsync()
    {
        var msg = new MqttApplicationMessageBuilder()
                .WithTopic("/" + ipTopic.text)
                .WithPayload(ipMessage.text)
                .WithExactlyOnceQoS()
                .Build();
        await client.PublishAsync(msg);
    }


    private void OnConnected(object sender, MqttClientConnectedEventArgs e)
    {
        Debug.Log($"On Connected: {e}");
    }

    private void OnDisconnected(object sender, MqttClientDisconnectedEventArgs e)
    {
        Debug.Log($"On Disconnected: {e}");
    }

    private void OnApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
    {
        sb.Clear();
        sb.AppendLine("Message:");
        sb.AppendFormat("ClientID: {0}\n", e.ClientId);
        sb.AppendFormat("Topic: {0}\n", e.ApplicationMessage.Topic);
        sb.AppendFormat("Payload: {0}\n", Encoding.UTF8.GetString(e.ApplicationMessage.Payload));
        sb.AppendFormat("QoS: {0}\n", e.ApplicationMessage.QualityOfServiceLevel);
        sb.AppendFormat("Retain: {0}\n", e.ApplicationMessage.Retain);

        Debug.Log(sb);
        msgText += Encoding.UTF8.GetString(e.ApplicationMessage.Payload) + "\n";
    }
}