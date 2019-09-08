# MJStreamingPlayer Assetについて
      
## はじめに
  - これは何？
    - Motion JPEG(MJPEG/MJPG)のストリームをUnityテクスチャ上で再生するAssetです。
  - 特徴
    - スレッド利用およびメモリ利用の最適化により、低遅延、ハイパフォーマンス、低メモリ消費を実現しています。
    - libjpeg-turboの利用により、高速なJPEGデコードを実現しています。
    - Gear VRでの利用が可能です。
    - Unityエディター上でも同様に再生可能です。
  - 用途の例
    - 比較的低遅延(通常0.5秒以下）のため、テレビ電話やラジコンのリアルタイム操作、テレプレゼンスのような双方向性のある用途に利用可能です。
    - たとえば、Raspberry Piを使ってカメラ映像をmjpg_streamerで配信し、ネット接続された端末で、Unityの自作プログラムで再生することができます。この場合、Raspberry PiカメラやUSBカメラ、さらにTHETA SのUSBライブビューなども利用できます。
  - 対応プラットフォーム
    - Android/GearVR
    - Windows 32/64bit
  - 対応Unityバージョン
    - Unity3D v5.3以降
  - このAssetを利用するには、MJPEGをストリーミングするサーバーが必要です。
    - サーバープログラムの例（確認済み）
      - Linux用
        - [mjpg_streamer](https://github.com/jacksonliam/mjpg-streamer)
      - Windows用
        - [GMax IP Camera (MJPG版）](http://www.gmax.ws/app.html)
        - [webcamXP 5](http://www.webcamxp.com/home.aspx)
      - カメラデバイス
        - RICOH THETA S (Wi-fi接続、プレビューモード) ※THETA Sの仕様により、低画質となります。

## 利用方法
  1. 本Assetをプロジェクトにインポートしてください。
  2. 再生対象マテリアルを持つGameObjectに、MJStreamingPlayerコンポーネントを追加してください。
     - 現在の仕様では、メインのマテリアルが持つメインのテクスチャとして、動画テクスチャがセットされます。
     - 特にこだわりがなければ、シェーダは Unlit/Texture にしてください。デフォルトの Standard シェーダも利用可能です。
  3. MJStreamingPlayerコンポーネントを設定してください。
     - ServerUrl: MJPGストリーミングサーバーのストリーミングURLをセットしてください。
     - PlayAutomatically: 自動で再生開始する場合はONにしてください。OFFの場合は、スクリプトからStartStreaming() を呼ぶ必要があります。
  4. 上記GameObjectが見えるようにCameraを設定し、UnityでPlayしてください。

## リファレンス
  - MJStreamingPlayerコンポーネント
    - プロパティ
      - serverUrl
        MJPGストリーミングサーバーのURLを指定してください。
      - autoReconnect
        エラー時に自動的に再接続するかどうかを指定します。
        ただし、ネットワークが切断されたケースや、サーバーがコネクションを維持したまま送信が停止したケースなど、再接続しない場合もあります。
    - メソッド
      - StartStreaming()
        serverUrlに設定されたサーバーに接続し、ストリーミング再生を開始します。      
      - StopStreaming()
        ストリーミング再生を停止します。

## 動作
  - フレームレートはサーバーが送信する速度に依存します。
  - 無駄なデコードを避けるため、表示可能なフレームレート以上のデコードは行わず、スキップします。
  - テクスチャサイズはサーバーが送信するMJPGの画像サイズに依存します。

## サーバーURL設定例
  - mjpg_streamerの場合
    - http://ホストのIPアドレス:ホストのポート番号/?action=stream
    - 例:
      - http://192.168.1.10:8080/?action=stream
  - GMax IP Camera (MJPG版)の場合
    - http://ホストのIPアドレス:ホストのポート番号/
    - 例:
      - http://192.168.1.10:8080/
  - webcamxp 5の場合
    - http://ホストのIPアドレス:ホストのポート番号/cam_1.cgi
    - 例:
      - http://192.168.1.10:8080/cam_1.cgi
  - ホストのIPアドレス、ポート番号の求め方
    - LAN内の場合
      - IPアドレス
        サーバープログラムを実行しているPCのLAN上でのIPアドレスを取得します。
      - ポート番号
        サーバープログラムによって、デフォルトのポート番号は異なります。サーバープログラムのドキュメントを参照してください。
    - インターネット上のサーバーの場合
      - IPアドレス、ポート番号
        ネットワークの知識がなければなかなか難しいことですが、mjpegサーバーとなるPCが、グローバルIP経由でアクセスできる状況を作成してください。
        上記サーバーにアクセスできるグローバルIPと、サーバープログラムが動作しているポート番号を利用します。

## 謝辞
  実装にあたってはのしぷさんのブログをだいぶ参考にさせて頂きました。感謝いたします！
  http://noshipu.hateblo.jp/entry/2016/04/21/183439

## データ通信料金に関するご注意
  本Assetでは、MJPGストリーミング時だけでなく、大容量ファイルが置かれたURLを接続先に指定したケースなど、大量のデータ転送を発生させる可能性があり、特に従量課金制のネットワークをご利用の場合、課金が高額となる恐れがありますのでご注意ください。
  なお、そのような場合でも本Assetの作者は課金に関する責任を負いませんのでご了承ください。

## ライセンスについて
  - このバージョンはプレビューバージョンです。
    - Unityでビルドされた実行形式での配布は可能です。
    - 本Assetの内容が取得できる形での再配布（内容を改変した場合を含む）は禁止です。
    - 商用利用は禁止としますが、お問い合わせください。
      - お問い合わせ先:
        - twitter: @hammmm
        - メール: ham.lua@gmail.com