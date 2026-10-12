# SensorSendingWeb — スマホセンサーの送信・中継

ARWhackAMoleに同梱したWebです。スマホのブラウザから加速度・回転速度・姿勢などを送り、UnityとPCモニターで赤・青別に受信します。Unityの起動を含む全体の手順は[リポジトリのREADME](../README.md)を参照してください。

```text
赤スマホ ─┐                  ┌─ Unity Editor / PCアプリ
          ├─ WebSocket中継 ──┤
青スマホ ─┘   Node.js / ws    └─ PCのWebモニター
```

通常のセンサー入力はメモリ上で中継し、DBは使用しません。サーバーを止めると接続状態は消えます。実験ページの「PCに保存」を押した場合だけ、記録をローカルの `data/experiments/` に保存します。

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
3. 「スマホの実センサー」を選び、「センサーを許可して送信」を押す。
4. iPhone Safariでは運動・方向アクセスを許可する。
5. 送信パケット数が増えることを確認し、「Unityからの画面・音・振動の合図を許可」をオンにする。
6. UnityをPlayする。設定は `ws://127.0.0.1:8787/ws`、同じルームキー。

画面を開いたまま利用します。スリープやバックグラウンド中の測定・送信は利用しません。スマホ側の `localhost` はスマホ自身なのでPCには繋がりません。スマホのセンサー取得にはHTTPSが必要です。

Quick TunnelはPCとトンネルが動いている間だけ利用できる一時URLです。再起動するとURLが変わります。GitHubはコードの共有先で、このNode中継を動かすホスティングではありません。

## ページ

| パス | 用途 |
|---|---|
| `/` | スマホの通常センサー送信・ゲーム用チャージ／往復回数入力・Unityの合図 |
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

腕回しの有効時間と、軽く振って戻る往復回数を `sensors.motionGame` に載せます。Unityの回数対決は加速度の最大値で競いません。従来の単発shake・傾き・声・tapイベントと、実験用ジャンプ／円候補は別入力です。現在の閾値は実機調整の仮設定です。

Androidは振動API・画面・音でUnityからの合図を通知し、iPhone Safariは画面・音を利用します。端末が合図を受け取ったACKと振動APIの戻り値は、実際の物理振動を保証しません。

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
