using System;

namespace SensorBridge
{
    [Serializable] public class VectorReading { public bool valid; public double timestampMs; public float x, y, z; }
    [Serializable] public class RotationReading { public bool valid; public double timestampMs; public float alpha, beta, gamma; }
    [Serializable] public class OrientationReading { public bool valid; public double timestampMs; public float alpha, beta, gamma, relativeBeta, relativeGamma; }
    [Serializable] public class CompassReading { public bool valid; public double timestampMs; public float degrees; }
    [Serializable] public class LightReading { public bool valid; public double timestampMs; public float lux; }
    [Serializable] public class BrightnessReading { public bool valid; public double timestampMs; public float value; }
    [Serializable] public class MicrophoneReading { public bool valid; public double timestampMs; public float rms; }
    [Serializable] public class TouchReading { public bool valid; public double timestampMs; public float x, y, pressure; public int count; }
    [Serializable] public class GameMotionReading { public bool valid,linear,circleActive; public double timestampMs,windowMs,peakAcceleration,circleSeconds,shakeAgeMs,shakeSpanMs; public int shakeVersion,shakeCount; }
    [Serializable] public class FeedbackAck { public string type,player,id; public bool enabled,screenVisible,vibrationSupported,vibrationAccepted; public double receivedAtMs; }
    [Serializable] public class FeedbackCommand { public string type="feedback",player,kind,id,message; public double durationMs; }
    [Serializable] public class SensorValues
    {
        public VectorReading acceleration, gravityAcceleration;
        public RotationReading rotationRate;
        public OrientationReading orientation;
        public CompassReading compass;
        public LightReading light;
        public BrightnessReading cameraBrightness;
        public MicrophoneReading microphone;
        public TouchReading touch;
        public GameMotionReading motionGame;
    }
    [Serializable] public class Gesture { public string kind; public float value; public double timestampMs; }
    [Serializable] public class SensorPacket
    {
        public string type, player;
        public int version;
        public long seq;
        public double timestampMs, receivedAtMs;
        public bool simulated;
        public SensorValues sensors;
        public Gesture[] gestures;
    }
    [Serializable] public class PlayerPresence { public string player; public bool connected; }
    [Serializable] public class Envelope { public string type; public PlayerPresence[] players; }
}
