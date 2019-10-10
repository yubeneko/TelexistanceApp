using UnityEngine;
using System.Net.Sockets;

public class UdpSender : MonoBehaviour
{
    public string remoteHost = "";
    public int remotePort = 60000;
    
    UdpClient udpClient;

    public void Init()
    {
        udpClient = new UdpClient();
    }

    public void SendData (string data)
    {
        byte[] sendBytes = System.Text.Encoding.ASCII.GetBytes(data);

        try
        {
            udpClient.Send(sendBytes, sendBytes.Length, remoteHost, remotePort);
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
