using UnityEngine;
using System.Net.Sockets;

public class UdpSender : MonoBehaviour
{
    public string remoteHost = "192.168.10.86";
    public int remotePort = 60000;
    
    UdpClient udpClient;

    void Start()
    {
        udpClient = new UdpClient();

        SendData("hello");
        SendData("Oh.");
    }

    void SendData (string data)
    {
        byte[] sendBytes = System.Text.Encoding.ASCII.GetBytes(data);

        try
        {
            udpClient.Send(sendBytes, sendBytes.Length, remoteHost, remotePort);
            Debug.Log("Data was sent.");
        }
        catch (SocketException se)
        {
            Debug.LogError(se.ToString());
            Debug.LogError($"Error Code : {se.ErrorCode}");
        }
    }

    void OnApplicationQuit()
    {
        udpClient.Close();
        Debug.Log("udp was closed.");
    }
}
