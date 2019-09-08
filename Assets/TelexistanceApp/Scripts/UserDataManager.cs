using UnityEngine;

public class UserDataManager : MonoBehaviour
{
    public GameObject cameraObject;
    UdpSender udpSender;
    HeadRotation headRotation;
    void Start()
    {
        udpSender = GetComponent<UdpSender>();
        headRotation = cameraObject.GetComponent<HeadRotation>();
        udpSender.Init();
    }

    void Update()
    {
       udpSender.SendData(headRotation.GetServoAngle()); 
    }
}
