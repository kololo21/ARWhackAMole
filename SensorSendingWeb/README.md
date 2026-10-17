# SensorSendingWeb — スマホセンサーの送信・中継

ARWhackAMoleに同梱したWebです。スマホのブラウザから加速度・回転速度・姿勢などを送り、UnityとPCモニターで赤・青別に受信します。Unityの起動を含む全体の手順は[リポジトリのREADME](../README.md)を参照してください。

```text
赤スマホ ─┐                  ┌─ Unity Editor / PCアプリ
          ├─ WebSocket中継 ──┤
青スマホ ─┘   Node.js / ws    └─ PCのWebモニター
```

通常のセンサー入力はメモリ上で中継し、DBは使用しません。サーバーを止めると接続状態は消えます。実験ページの「PCに保存」を押した場合だけ、記録をローカルの `data/experiments/` に保存します。

## 通常プレイと診断

`/` はスマホのコントローラー画面です。QRから開き、送信開始と必要なマイク許可を行ったら、ページを前面にしたままUnityの画面を見て遊びます。傾きの再基準化・声の調整はUnityのSTART前の画面で行います。

グラフ・センサー値・しきい値・カメラ・タッチ・合成入力・JSONは `/diagnostics` に移動しました。既存の `/monitor`（PC管理）と `/lab`（実験記録）は引き続き利用できます。診断画面の声イベントしきい値は実験用で、ゲームのおねむはUnity側が生RMSから判定します。

## 起動

Node.js 20以上を使用します。リポジトリ直下から実行します。

```sh
cd SensorSendingWeb
npm ci
npm start
```

PCモニターは `http://localhost:8787/monitor#room=demo-room-change-me-2026`。Unityの初期ルームと一致しています。ルームキーを変える場合は、Unityの `GameManager → Whack Sensor Game → Room Key` も変更してください。

別ターミナルで同じフォルダに移動し、スマホ用HTTPS URLを発行します。

```sh
# cloudflaredがないMacでは初回だけ実行
brew install cloudflared
npm run share
```

他のOSではcloudflaredを導入してPATHへ置くか、`CLOUDFLARED_PATH` に実行ファイルのパスを指定します。`npm run share` は自動インストールしません。

1. 発行された `https://….trycloudflare.com` をモニターの「スマホ用HTTPS URL」へ入力する。
2. 赤・青のQRをスマホで読み取る。
3. 「センサーを許可して送信」を押す。
4. iPhone Safariでは運動・方向アクセスを許可する。
5. スマホの「送信中」とPCモニターの受信を確認する。おねむには「おねむ用のマイクを許可」も押す。
6. UnityをPlayする。設定は `ws://127.0.0.1:8787/ws`、同じルームキー。

画面を開いたまま利用します。スリープやバックグラウンド中の測定・送信は利用しません。スマホ側の `localhost` はスマホ自身なのでPCには繋がりません。スマホのセンサー取得にはHTTPSが必要です。

Quick TunnelはPCとトンネルが動いている間だけ利用できる一時URLです。再起動するとURLが変わります。GitHubはコードの共有先で、このNode中継を動かすホスティングではありません。

## ページ

| パス | 用途 |
|---|---|
| `/` | スマホの通常送信・腕回しの時間／回転量・マイク音量 |
| `/diagnostics` | グラフ・詳細値・実験用しきい値・合成入力・合図の診断 |
| `/monitor` | PCで赤青の値を表示、ルーム・HTTPS URLを指定してQR発行 |
| `/lab` | 動作を記録し、端末JSON保存／PC保存で閾値調整 |
| `/health` | 中継の稼働確認 |
| `/ws` | WebSocketエンドポイント |

## 取得情報

- 加速度XYZ、重力込み加速度XYZ（m/s²）。
- 回転速度α／β／γ（deg/s）、姿勢・基準からの傾き（deg）。
- 方位（対応端末のみ）、照度lux（AmbientLightSensor対応環境のみ）。
- 任意追加：カメラ平均輝度0〜1、マイクRMS音量、タッチ位置・圧力・接触数。

未取得値は `valid:false`／`null` で区別します。照度とカメラ輝度は別の値です。カメラ・マイクを利用する場合も、映像や音声そのものを送らず、端末で計算した数値だけを送ります。

腕回しの有効時間と回転量を `sensors.motionGame` に載せる。通常チャージは有効時間、5秒対決は腕回し判定が通った間の回転量を使い、加速度の最大値では競わない。`circleVersion:1` と `circleDegrees` が新形式。変更後は中継の再起動・両スマホの再読み込み・UnityのPlay再開始が必要。旧送信は通常チャージを維持できるが腕回し対決の開始対象にしない。従来の往復shake・傾き・声・tap、実験用ジャンプ／円候補は別入力として残し、往復の300ms下限を腕回しの採点には使わない。

通常プレイの案内と音はUnity側に表示・再生します。スマホの通常画面は通信と初期の権限操作が中心です。診断ページでは従来の画面・音・Androidの振動を試せますが、ACKや振動APIの戻り値は実際の物理振動を保証しません。

## 設定

| 環境変数 | 初期値・用途 |
|---|---|
| `PORT` | `8787`（中継とshareで同じ値にする） |
| `HOST` | `127.0.0.1` |
| `CLOUDFLARED_PATH` | cloudflaredの実行ファイル。省略時は `.tools/cloudflared` またはPATHから探す |
| `TLS_CERT`／`TLS_KEY` | 信頼済み証明書を用いて直接HTTPSにする場合のファイルパス |

## プロトコル

接続直後にjoinを送り、同じルームで送信者と受信者を対応させます。

```json
{"type":"join","role":"sender","room":"demo-room-change-me-2026","player":"red"}
```

受信者は `role:"receiver"`。ルームは英数字・`_`・`-`の16〜64文字で、赤・青各1送信者までです。`sample` はバージョン・連番・時刻・センサー・イベントを持ちます。中継が送信元チームとサーバー受信時刻を付けます。

Unityからは `feedback`、スマホからは `feedbackAck` を返します。再接続で過去の撃破入力を再送しません。新しく参加した受信者へ渡す直近値では、攻撃イベントとチャージ／回数を積み増す入力を除外します。

## 動作確認

```sh
npm run build
npm run lint
npm test
```

静的HTML/CSS/ES modulesなのでバンドルは不要です。build/lintはJS構文・JSON・ページ参照を確認し、テストは中継、権限、ルーム分離、連番重複、切断、合図、センサー判定を確認します。

任意で、発行したURLのHTTPSとWebSocketを合成データで確認できます。

```sh
node scripts/public-smoke.js https://実際に発行されたホスト.trycloudflare.com
```

実験の記録手順は[EXPERIMENTS.md](EXPERIMENTS.md)。実測ファイル、node_modules、cloudflared実行ファイル、証明書、ローカル設定はGit管理対象外です。
