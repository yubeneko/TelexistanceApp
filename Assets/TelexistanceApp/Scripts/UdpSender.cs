using UnityEngine;
using System.Net.Sockets;

public class UdpSender
{
    string _remoteHost = "";
    int _remotePort = 60000;
    
    UdpClient _udpClient;

    public UdpSender(string remoteHost, int remotePort)
    {
        _remoteHost = remoteHost;
        _remotePort = remotePort;
        _udpClient = new UdpClient();
    }

    public void SendData (string data)
    {
        byte[] sendBytes = System.Text.Encoding.ASCII.GetBytes(data);

        try
        {
            _udpClient.Send(sendBytes, sendBytes.Length, _remoteHost, _remotePort);
        }
        catch (SocketException se)
        {
            Debug.LogError(se.ToString());
            Debug.LogError($"Error Code : {se.ErrorCode}");
        }
    }

    public void UdpClientClose()
    {
        _udpClient.Close();
        Debug.Log("udp was closed.");
    }
}
