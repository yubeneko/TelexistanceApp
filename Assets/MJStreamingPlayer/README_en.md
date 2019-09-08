# MJStreamingPlayer Asset

## Introduction
  - What is this?
    - Asset that plays Motion JPEG (MJPEG / MJPG) stream on Unity texture.
  - Features
    - Low latency, high performance, low memory consumption are realized by optimizing thread usage and memory usage.
    - By using libjpeg - turbo, high - speed JPEG decoding is realized.
    - It is possible to use it with Gear VR.
    - You can play stream equally on the Unity Editor.
  - Examples of uses
    - Relatively low delay (usually less than 0.5 seconds), it can be used for bidirectional applications such as videophone and radio control real-time operation, telepresence.
    - For example, you can use Raspberry Pi to distribute camera images with mjpg_streamer and play back with Unity's own program on a net connected devices. In this case, you can also use Raspberry Pi camera, USB camera, THETA S USB live view, etc.
  - Supported platforms
    - Android / GearVR
    - Windows 32/64 bit
  - Supported Unity version
    - Unity3D v5.3 or later
  - To use this Asset, you need a server to stream MJPEG.
    - Server program example (confirmed)
      - For Linux
        - [mjpg_streamer] (https://github.com/jacksonliam/mjpg-streamer)
      - For Windows
        - [GMax IP Camera (MJPG version)] (http://www.gmax.ws/app.html)
        - [webcamXP 5] (http://www.webcamxp.com/home.aspx)
      - Camera device
        - RICOH THETA S (Wi-fi connection, preview mode) ※ The image quality will be lowered by THETA S specification.

## How to Use
  1. Please import this Asset into the project.
  2. Add the MJStreamingPlayer component to the GameObject that contains the material to be played back.
     - In the current specification, the animated texture is set as the main texture of the main material.
     - If you are not particular about it, please set the Unlit/Texture shader. A default Standard shader is also ok.
  3. Configure the MJStreamingPlayer component.
     - ServerUrl: Please set the streaming URL of the MJPG streaming server.
     - PlayAutomatically: Set this to ON to start playback automatically. If it is OFF, you need to call StartStreaming () from the script.
  4. Set Camera so that the above GameObject can be seen, and then play with Unity.

## reference
  - MJStreamingPlayer component
    - Properties
      - serverUrl
        Please specify the URL of the MJPG streaming server.
      - autoReconnect
        Specify whether to reconnect automatically on error.
        However, there are cases where you do not reconnect, such as when the network is disconnected, when the transmission is stopped while the server maintains the connection.
    - Methods
      - StartStreaming ()
        Connect to the server set to serverUrl and start streaming playback.
      - StopStreaming ()
        Stop streaming playback.

## Details
  - Frame rate depends on the speed the server sends.
  - To avoid unnecessary decoding, this asset will not decode more than the displayable frame rate and will skip frames.
  - The texture size depends on the image size of MJPG sent by the server.

## Server URL Setting Example
  - For mjpg_streamer
    - http://host-IP-address:host-port-number/?action=stream
    - Example:
      - http://192.168.1.10:8080/?action=stream
  - For GMax IP Camera (MJPG version)
    - http://host-IP-address:host-port-number/
    - Example:
      - http://192.168.1.10:8080/
  - webcamxp 5 case
    - http://host-IP-address:host-port-number/cam_1.cgi
    - Example:
      - http://192.168.1.10:8080/cam_1.cgi
  - How to obtain the host's IP address and port number
    - In case of LAN
      - IP address
        Acquire the IP address on the LAN of the PC running the server program.
      - port number
        Depending on the server program, the default port number will be different. See the documentation of the server program.
    - For servers on the Internet
      - IP address, port number
        It is rather difficult if you do not have network knowledge, but please create a situation where PC which becomes mjpeg server can access via global IP.
        Please use the global IP that can access the above server and the port number where the server program is running.

## Thanks
  I thank noshipu, as I referred to this page when implementing mjpeg decoding.
  http://noshipu.hateblo.jp/entry/2016/04/21/183439

## Note on data communication charges
  When using this Asset, there is a possibility of generating a large amount of data transfer, not only when MJPG streaming but also when using a URL where a large file is placed as a connection destination, especially if you use a network with a pay-as-you-go. Please be careful as there is a risk that the charge will be expensive.
  Please note that the author of this asset will not be responsible for billing even in such a case.

## Lisence
  - This version is a preview version.
    - Distribution in executable format built with Unity is possible.
    - Redistribution (including cases where content has been altered) in a form that can acquire the contents of this Asset is forbidden.
    - Commercial use is forbidden, but please contact us.
      - Contact:
        - twitter: @hammmm
        - email: ham.lua@gmail.com
