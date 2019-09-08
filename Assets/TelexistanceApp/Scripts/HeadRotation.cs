using UnityEngine;

public class HeadRotation : MonoBehaviour
{
    public string GetServoAngle ()
    {
        return $"{ConvertPitch(transform.localEulerAngles.x)}/{ConvertYaw(transform.localEulerAngles.y)}";
    }

    int ConvertPitch (float unityAngleX)
    {
        var servoPitch = 0f;

        if (270f <= unityAngleX && unityAngleX < 360f)
        {
           servoPitch = unityAngleX - 270f;
        }
        else if (0f <= unityAngleX && unityAngleX <= 90f)
        {
            servoPitch = unityAngleX + 90f;
        }

        return (int)servoPitch;
    }

    int ConvertYaw (float unityAngleY)
    {
        var servoYaw = 0f;

        if (0f <= unityAngleY && unityAngleY < 90f)
        {
            servoYaw = 90f - unityAngleY;
        }
        else if (90f <= unityAngleY && unityAngleY < 180f)
        {
            servoYaw = 0f;
        }
        else if (180f <= unityAngleY && unityAngleY < 270f)
        {
            servoYaw = 180f;
        }
        else
        {
            servoYaw = 270f - (unityAngleY - 180f);
        }

        return (int)servoYaw;
    }
}