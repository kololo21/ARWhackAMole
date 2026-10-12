export class Feedback {
  constructor(onMessage) {this.onMessage=onMessage;this.audio=null;this.enabled=false;this.last=-Infinity;this.audioError='';}
  async enable() {
    this.enabled=true;
    try {this.audio??=new (window.AudioContext||window.webkitAudioContext)();await this.audio.resume();this.audioError='';}catch(e){this.audioError=e.name;}
  }
  tone(kind) {
    if(!this.enabled||document.hidden)return '音：停止中';
    if(this.audio?.state!=='running')return `音：${this.audioError||this.audio?.state||'未準備'}`;
    try{
      const osc=this.audio.createOscillator(),gain=this.audio.createGain(),t=this.audio.currentTime;
      osc.frequency.value=kind==='error'?260:880;gain.gain.setValueAtTime(.08,t);gain.gain.exponentialRampToValueAtTime(.001,t+.13);
      osc.connect(gain).connect(this.audio.destination);osc.start(t);osc.stop(t+.14);
      return '音：再生要求済み';
    }catch(e){return `音：${e.name}`;}
  }
  // Vibration must never wait for the audio resume promise, which may stay pending.
  test({vibrate=true,sound=true,pattern}={}) {
    this.enabled=true;
    const ready=sound?this.enable():null;
    this.show('test',{vibrate,sound:false,pattern});
    const ticket=this.last,message=this.lastResult;
    if(sound){
      this.onMessage?.(`${message} / 音：準備中`);
      ready.then(()=>{if(this.last===ticket&&!document.hidden)this.onMessage?.(`${message} / ${this.tone('test')}`);});
    }
  }
  show(kind='hit', {vibrate=true,sound=true,pattern}={}) {
    const now=performance.now();if(now-this.last<180&&!['warning','countdown','contest'].includes(kind))return null;this.last=now;
    const patterns={hit:[60],ready:[80,60,80],error:[160],test:[300,150,300],warning:[100],countdown:[80,60,80],contest:[0]};
    const names={hit:'チャージ消費',ready:'合図',error:'中止',test:'振動テスト',warning:'まもなく瞬間出現',countdown:'シェイク勝負の準備',contest:'5秒間シェイク！'};
    let vibrationAccepted=false;
    let result='振動OFF';
    if(vibrate){
      if(!this.enabled)result='振動：合図の許可が必要です';
      else if(document.hidden)result='振動：画面が非表示です';
      else
      if(typeof navigator.vibrate==='function') {
        const request=pattern??patterns[kind]??patterns.hit;
        try{
          const accepted=navigator.vibrate(request);
          vibrationAccepted=accepted;
          result=`振動API：あり / 戻り値：${accepted} / 指定：${JSON.stringify(request)} ms / 操作：${navigator.userActivation?.hasBeenActive===false?'未確認':'済'} / ${window.top===window?'直接ページ':'埋め込みページ'} / ${accepted?'要求を受付（実振動は未確認）':'ブラウザが拒否'}`;
        }catch(e){result=`振動API：あり / エラー：${e.name}`;}
      }else result='このブラウザは振動API非対応';
    }
    this.lastResult=`${names[kind]??kind} / ${result}${sound?' / '+this.tone(kind):''}`;
    this.onMessage?.(this.lastResult);
    return {vibrationAccepted,vibrationSupported:typeof navigator.vibrate==='function',shown:!document.hidden};
  }
}
