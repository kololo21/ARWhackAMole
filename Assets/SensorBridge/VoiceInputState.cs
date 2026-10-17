using System;
using System.Collections.Generic;

namespace SensorBridge
{
    // RMS only; it detects a sustained sound onset, not the spoken words.
    public sealed class VoiceInputState
    {
        private readonly List<double> noise = new List<double>();
        private double calibrationStart=-1,lastReading=-1,quietStart=-1,loudStart=-1,lastEvent=-100000;
        private bool armed;
        public bool Calibrated {get;private set;}
        public bool Verified {get;private set;}
        public double Baseline {get;private set;}
        public double Sensitivity {get;private set;} = 1;
        // Microphone gains vary: ordinary speech can be only RMS 0.003.
        // Keep thresholds relative to measured room noise, with small floors near silence.
        public double Threshold => Math.Max(Math.Max(Baseline*1.4,Baseline+.0002),Math.Max(.001,Baseline*2.5)*Sensitivity);
        public double QuietThreshold => Math.Min(Threshold*.75,Math.Max(Baseline*1.3,Baseline+.0001));
        public bool Armed => armed;
        public long QuietSeq {get;private set;} = -1;
        public double Progress {get;private set;}
        public void AdjustSensitivity(double factor)
        {
            Sensitivity=Math.Max(.4,Math.Min(2,Sensitivity*factor));Verified=false;ResetDetection();
        }
        public void Recalibrate()
        {
            Calibrated=Verified=false;calibrationStart=-1;lastReading=-1;noise.Clear();ResetDetection();
        }
        public void ResetDetection()
        {
            quietStart=loudStart=-1;armed=false;QuietSeq=-1;Progress=0;
        }
        public bool Observe(long seq,double readingMs,double rms,bool paused)
        {
            if(readingMs<=lastReading)return false;
            if(lastReading>=0&&readingMs-lastReading>500)
            {
                ResetDetection();
                if(!Calibrated){noise.Clear();calibrationStart=-1;}
            }
            lastReading=readingMs;
            if(!Calibrated)
            {
                if(calibrationStart<0)calibrationStart=readingMs;
                noise.Add(rms);
                Progress=Math.Min(1,(readingMs-calibrationStart)/2000);
                if(readingMs-calibrationStart>=2000&&noise.Count>=20)
                {
                    noise.Sort();Baseline=noise[noise.Count/2];Calibrated=true;noise.Clear();Progress=0;
                }
                return false;
            }
            if(paused){ResetDetection();return false;}
            if(rms<QuietThreshold)
            {
                loudStart=-1;Progress=0;
                if(quietStart<0)quietStart=readingMs;
                if(readingMs-quietStart>=300){armed=true;QuietSeq=seq;}
            }
            else
            {
                quietStart=-1;
                if(armed&&rms>=Threshold)
                {
                    if(loudStart<0)loudStart=readingMs;
                    Progress=Math.Min(1,(readingMs-loudStart)/100);
                    if(readingMs-loudStart>=100&&readingMs-lastEvent>=700)
                    {
                        armed=false;loudStart=-1;lastEvent=readingMs;Verified=true;return true;
                    }
                }
                else {loudStart=-1;Progress=0;}
            }
            return false;
        }
    }
}
