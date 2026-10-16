# ARWhackAMole

Vuforiaの穴マーカーを手で隠して遊ぶ、赤・青2チームのARもぐらたたき。Unityゲームとスマホのセンサー送信Webを同じリポジトリにまとめています。

- Unity：もぐらの出現・撃破、赤青の得点、BGM／効果音、チャージ・回数対決・瞬間出現。
- [SensorSendingWeb](SensorSendingWeb/README.md)：スマホのセンサー取得、WebSocket中継、PCモニター、実験記録。
- [センサーのゲーム仕様・検証](SENSOR_GAME.md)：チャージ条件、通知、コード構成と検証範囲。
- [実装計画・開発ガイド](AGENTS.md)：企画書と現状の対応、次の作業、完了条件、共同開発の進め方。

## 必要なもの

- Unity **6000.3.24f1**（Unity Hubからインストール）。
- Node.js **20以上**、npm。
- Git LFS。Prefab・音・日本語フォントなどの実ファイル取得に使用。
- スマホからアクセスするHTTPS URLを作るための `cloudflared`。macOSでは `brew install cloudflared` で導入できます。
- スマホ：Android Chrome／iPhone Safariを想定。

## 取得

スマホセンサー連携は `main` に含まれています。

```sh
git lfs install
git clone https://github.com/kololo21/ARWhackAMole.git
cd ARWhackAMole
git lfs pull
```

## 1. PCでスマホWebの中継を起動する

Unityを動かすPCのターミナルで、リポジトリ直下から実行します。

```sh
cd SensorSendingWeb
npm ci
npm start
```

別のターミナルでも同じ `SensorSendingWeb` フォルダに移動し、HTTPS公開を開始します。

```sh
npm run share
```

表示された `https://….trycloudflare.com` がスマホ用のURLです。サーバーとトンネルの両方を動かしたまま使います。トンネルを再起動するとURLが変わります。これは一時公開で、GitHubへのプッシュだけではWebは起動・常設デプロイされません。

## 2. スマホ2台で送信する

PCで次のモニターを開きます。公開コードには例示用のルームキーを指定しています。実際にはモニターで生成した16〜64文字のキーを使い、Unityにも同じ値を設定してください。

```text
http://localhost:8787/monitor#room=demo-room-change-me-2026
```

1. モニターの「スマホ用HTTPS URL」に `npm run share` のURLを入れる。
2. 赤・青のQRコードをそれぞれのスマホで読み取る。
3. データの取得元を「スマホの実センサー」にし、「センサーを許可して送信」を押す。iPhoneは運動・方向アクセスも許可する。
4. 「送信○パケット」の数が増えることを確認する。開くだけでは送信は始まりません。
5. 「Unityからの画面・音・振動の合図を許可」をオンにする。ページを前面に開いたまま使用する。

QRを使わない場合は、公開URLの末尾にそれぞれ追加します。

```text
/#room=demo-room-change-me-2026&player=red
/#room=demo-room-change-me-2026&player=blue
```

## 3. Unityでゲームを起動する

1. Unity Hubで **このリポジトリのルート**を開く。`SensorSendingWeb` はUnityプロジェクトではありません。
2. 必要に応じてVuforiaのLicense Keyを自身の環境に設定する。`Assets/Resources/VuforiaConfiguration.asset` はGit管理対象外なので、各PCで設定します。
3. `Assets/Scenes/SampleScene.unity` を開く。
4. Hierarchyの `GameManager` を選び、Inspectorの **Whack Sensor Game** を確認する。

| 設定 | 同じPCで中継とUnityを動かす場合 |
|---|---|
| Server Url | `ws://127.0.0.1:8787/ws` |
| Room Key | `demo-room-change-me-2026`（スマホ／モニターと一致させる） |
| Allow Simulated | オフ（実機用） |

5. ▶︎を押す。WebSocketへの接続は自動で開始する。
6. 下部の赤・青表示が「受信中」になったら連携成功。START前に「カメラの表示方向」で左右反転・上下反転を個別に選び、映像を確認してSTARTを押す。両方ONで180度回転。結果画面でも次のプレイに向けて変更できる。
7. 左手でスマホを持ち、腕回しでチャージ。傾きの基準を合わせ、おねむにはスマホでマイクを許可する。右手で穴マーカーを隠して倒す。

再生中の `GameManager` はHierarchyの `DontDestroyOnLoad` 配下に移動します。接続設定は **Whack Sensor Game** 側で指定します。再生を止めてRoom Key／Server Urlを変更し、再生し直してください。

ゲームの時間と得点は、既存 **Game Manager** コンポーネントの `Game Duration`／`Red Point`／`Blue Point` で設定します。現在の保存済みSceneは **180秒・各10点**。時間は `Game Duration` で調整できます。

## ゲームのセンサー機能

- 通常もぐら：チャージ不要。得点はもぐらの色のチームへ。
- ぜんまい：チャージ50を消費して30点。王様：75を消費して50点。消費量と前後の残量を2秒間表示します。
- ヘルメット：手を戻して2回叩く。バランス：30度傾けて0.6秒維持してから叩く。おねむ：マイクを許可し、声で起こしてから叩く。
- 敵の頭上に必要操作と得点を表示。詳細と初期調整値は[キャラクター別仕様](SENSOR_GAME.md)、作業計画は[GAMEPLAY_PLAN.md](GAMEPLAY_PLAN.md)。
- チャージ：腕回しの有効時間に応じて増加、最大100。接続するだけでは増えません。
- 回数対決：3秒の準備後、5秒の軽い往復回数で対戦。強さは加点しません。勝者にチャージ25。自動対決はゲーム内経過60秒ごと。対決中はゲーム時間・通常の出現・撃破を一時停止。
- スピード：PCの穴付近で約1秒予告し、約0.9秒出現。PCの音とスマホの合図を補助に使います。

既存のランダム出現、赤青の得点、BGM／効果音、タイトル・結果・リトライは維持しています。

## 繋がらない場合

- スマホの送信パケット数が増えない：送信開始とセンサー権限を確認し、ページを前面に戻す。
- 赤／青が「未接続」：スマホとUnityのRoom Keyが一致しているか確認する。
- 「接続済・センサー待ち」：接続はありますが、有効な新しいゲーム用センサー値が届いていません。スマホのページを再読み込みして実センサーで送信し直す。
- 「受信中」：接続完了してデータを受け取っている状態です。以前の「接続中」表示も同じ意味でした。
- Unityだけ繋がらない：同じPCならServer Urlは `ws://127.0.0.1:8787/ws`。ブラウザ用の `https://…` URLをそのまま入れない。
- Unityを別PCで動かす：Server Urlを `wss://公開URLのホスト/ws` にする。ローカルの `127.0.0.1` はそれぞれのPC自身を指します。
- `EADDRINUSE`：8787番ポートに既存の中継が起動しています。もう一つ起動せず、既存の中継を利用する。
- チャージが0：受信だけでは増えません。START後に腕回しをする。センサーの認識条件は仮調整で、機種や持ち方で差があります。
- 「チャージもぐらを出す」「合図→瞬間もぐら」が出ない：ゲーム中で、認識中の空いている穴が必要です。回数対決と瞬間出現には赤青両方の新しいセンサー値が必要です。

## 開発時の確認

```sh
cd SensorSendingWeb
npm run build
npm run lint
npm test
```

Unityでは `Sensor Game → Run Rules Checks`。合成データによる統合確認は、中継を起動した状態でリポジトリ直下から次を実行し、Unityで `Sensor Game → Run Integration Verification` を選びます。

```sh
node Tools/sensor-game-test-senders.mjs
```

検証専用ルームを使い、通常のプレイルームには合成データを流しません。検証終了後は送信スクリプトをCtrl+Cで停止します。結果は `Verification/` に保存されます。

ソフトウェア上の統合確認は合成センサー・追跡状態で成功。2026-10-12に利用者による実スマホからのゲームへの受信・チャージ増加も確認できました。スマホ2台の対戦・実際の手の遮蔽・物理振動を合わせた一連の実機試験は未実施です。

## Git管理するファイルとローカル生成物

シーン・Prefab・スクリプト・必要なアセットとそのmeta、ProjectSettings／Packages、Assets/StreamingAssets内のマーカーデータ、SensorSendingWebのソース・ロックファイル・READMEは共有します。

UnityのLibrary／Temp／Logs／UserSettings、生成されたcsproj／sln、VS Codeのローカル設定、QCARのローカル状態、Vuforiaの各PC用ライセンス設定とそのmeta、node_modules、実測記録、Verificationの再生成画像・結果はignoreしています。ライセンス設定は各PCで設定してください。ignore対象もローカルには残り、検証や記録で必要なときに再生成・利用できます。

2026-10-16：7種類のキャラ別ルール・倍率・操作表示・PCスピード予告を実装。Web18テストと実Sceneの合成統合確認が成功。実機でのセンサー認識とゲームバランスは要調整。
