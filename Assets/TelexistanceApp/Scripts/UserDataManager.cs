using UnityEngine;

public class UserDataManager : MonoBehaviour
{
    public GameObject cameraObject;
    public string remoteHost = "";
    public int remotePort = 60000;
    UdpSender udpSender;
    HeadRotation headRotation;
    void Start()
    {
        udpSender = new UdpSender(remoteHost, remotePort);
        headRotation = cameraObject.GetComponent<HeadRotation>();
    }

    void Update()
    {
       udpSender.SendData(headRotation.GetServoAngle()); 
    }

    void OnApplicationQuit()
    {
        udpSender.UdpClientClose();
    }
}
