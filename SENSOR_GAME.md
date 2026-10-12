# スマホセンサーとARWhackAMole

実装先はこのゲームリポジトリ。旧SensorSendingWeb/UnityDemoは使用しない。WebSocket受信・パケット・センサー状態管理だけをAssets/SensorBridgeへ移植した。

## 最新版との統合方針

2026-10-12にorigin/mainのfab3904（PR #3）を現在のagent/sensor-gameplayへfast-forwardで取り込んだ。既存のGameManager、HoleSlot、DwellerSpawner、UIController、AudioManagerをゲーム進行の本体として残した。センサー側の独自タイマー・得点・出現ループと旧MoleSensorLinkは使わない。通常・チャージ・瞬間もぐらも、すべて最新版のHoleSlotから出現・撃破する。

保存済みSampleSceneのGameManagerはgameDuration=180秒、各10点。利用者が調整した設定を共有版に反映した。時間はUnityのInspectorでGameManager.gameDurationを変更でき、対決は自動イベントと手動ボタンの両方で開始できる。

## 操作と得点

- 叩く操作は手で穴マーカーを隠す。最新版のHoleSlotの追跡ロストで撃破する。
- 手の持ち主は識別しない。赤もぐらは赤、青もぐらは青の得点。点数は既存GameManagerのredPoint/bluePoint（保存済み設定は各10点）を使う。
- 通常もぐらはチャージ不要。スマホ未接続でも倒せる。
- ぜんまいもぐら（Dweller_Windupの赤/青Prefab）だけ、もぐらの色のチームのチャージを25消費して倒す。他の通常Prefabには条件を追加しない。
- 不足・送信停止の場合は加点も消費もしない。穴を見える状態に戻し、腕を回してからもう一度隠す。
- 同じ出現の二重加点・二重消費、時間切れによる加点を防ぐ。
- 最新版のランダムな色・種類・出現間隔・寿命・同時出現上限、BGM、撃破/消滅/結果の効果音、タイトル・結果・リトライを維持する。

## センサーのゲーム性

- 腕回しの有効時間に応じてチャージ。20/秒、最大100。速さ・加速度による倍率はない。
- 3秒の準備後、5秒間の軽い往復回数で赤青が対戦。強さは勝敗に使わない。勝者にチャージ25、同点や通信途絶はボーナスなし。対決中は既存ゲームの残り時間、出現待ち、もぐらの寿命と撃破を停止する。
- 瞬間もぐら：認識中の空いた穴を予約。両スマホへ合図を送り、画面表示の返答を確認して500ms後にSpeedyを350ms表示する。返答なし・送信途絶では出さない。
- Androidは振動APIに加えて画面・音の合図、iPhone Safariは画面・音。ACKとvibrateの戻り値は物理的な振動を保証しない。
- 2台の新しい送信がある場合、20秒のゲーム時間ごとに回数勝負、12〜22秒ごとに瞬間もぐらを試す。下部のボタンからも開始できる。
- 「チャージもぐらを出す」「合図→瞬間もぐら」は認識中の空いている穴を使う。穴が全部使用中なら、引っ込むまで待つ。

## 起動

1. Unity 6000.3.24f1でこのリポジトリのAssets/Scenes/SampleScene.unityを開く。
2. 同梱SensorSendingWebのNode中継（8787）を起動する。DBは不要。
3. スマホの通常送信ページを再読み込みし、同じルームで赤・青を1台ずつ「センサーを許可して送信」。Unityの合図も許可する。/labは記録用。
4. ルームはdemo-room-change-me-2026、同じMacのUnityはws://127.0.0.1:8787/ws。
5. Play→既存START。上部は既存の赤青得点・時間、下部はチャージ・往復回数・接続状態。
6. マーカーを認識させ、手で隠して撃破。専用もぐらを倒す前に腕回しでチャージする。

保存済みSceneの新しいGameManagerへWhackSensorGameを追加済み。URL・ルーム・自動イベントは同コンポーネントのInspectorで設定する。時間・得点は既存GameManagerのInspectorで設定する。カタログはAssets/Resources/WhackMoleCatalog.asset。Sensor Game/Prepare Mole Catalogで作成できる。

## コード

- Assets/SensorGame/WhackSensorGame.cs：既存ゲームへのセンサー用アダプター。独自の得点やゲームタイマーを持たない。
- Assets/SensorGame/WhackSensorRules.cs：SensorGamePolicy（チャージ専用撃破の条件・勝者ボーナス）。
- Assets/Script/Game/HoleSlot.cs：既存の穴・Prefab・アニメーション・手による撃破。チャージ条件と対決中の寿命停止を追加。
- Assets/Script/Game/GameManager.cs：既存の状態・得点・時間。対決中の停止フラグを追加。
- Assets/Script/Game/DwellerSpawner.cs：既存ランダム出現。対決中の待ち時間停止と瞬間出現用の穴予約を追加。
- Assets/SensorBridge：Nodeと同じパケットを扱う標準.NET WebSocket。合成データは通常モードで採用しない。
- Assets/Resources/WhackSensorJP.otf：日本語フォント。WhackSensorFont-OFL.txtを同梱。

## 検証

Sensor Game/Run Rules Checksでセンサー条件・充電・往復回数対決・通信途絶を確認する。Sensor Game/Run Integration Verificationは実際の最新版SampleScene、GameManager、HoleSlot、Prefab、得点UIを使い、専用ルームの合成センサーと追跡状態の再現で確認する。旧デモは使わない。未保存Sceneがある場合は実行しない。

統合検証前に、SENSOR_WEB_ROOTを別のWeb作業フォルダを使う場合だけ指定する。同梱Webならリポジトリ直下からnode Tools/sensor-game-test-senders.mjsを実行する。結果と画面はVerification/。通常の実験ルームに合成データは送らない。検証用の時間・カメラ位置・追跡設定はPlay停止時に戻り、保存済みの時間設定とマーカー配置を変更しない。

実際の手・カメラ・Android/iPhoneを合わせた試験は未実施。現在の入口は既存のマーカー追跡ロストなので、手とカメラ外への移動は区別できない。センサー閾値は実機調整の仮設定で、安全限界の数値ではない。出現タイミングはネットワーク遅延の影響を受ける。

2026-10-12の最終確認：センサー条件チェックと最新版fab3904のSampleSceneでの統合確認に成功。10点の色別得点、チャージ専用撃破、二重処理防止、受信でのチャージ、回数勝負・ゲーム時間停止と再開、合図ACK後のSpeedy出現・時間切れ、結果・リトライ、最新版のランダム出現の再開とBGM再生を確認した。ソフトウェア上の検証は合成センサー・追跡状態で行い、実機の手・スマホ・マーカーを合わせた試験は未実施。

2026-10-12：利用者が実スマホからこのゲームへのセンサー受信と腕回しでのチャージ増加を確認した。180秒の設定を共有版に反映した。公開版のルームキーは例示用とし、実際のキーは各自の環境で設定する。
