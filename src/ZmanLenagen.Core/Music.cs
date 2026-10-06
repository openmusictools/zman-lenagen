namespace ZmanLenagen.Core;
public record Scale(string Major,string Minor);
public static class Fifths {
 public static readonly IReadOnlyList<Scale> Keys=Array.AsReadOnly(new[]{
  new Scale("C","Am"),new("G","Em"),new("D","Bm"),new("A","F♯m"),new("E","C♯m"),new("B","G♯m"),
  new("G♭ / F♯","E♭m / D♯m"),new("D♭","B♭m"),new("A♭","Fm"),new("E♭","Cm"),new("B♭","Gm"),new("F","Dm")});
}
// Audio samples, not UI ticks, determine every onset. Fractional sample positions
// are retained, so rounding cannot accumulate even after hours at an awkward BPM.
public sealed class ClickTrack {
 public const int SampleRate=48000;
 public int Bpm {get;}
 public int Beats {get;}
 public int Subdivision {get;}
 public int Sound {get;}
 public bool Accent {get;}
 public long Samples {get;private set;}
 public long Pulses {get;private set;}
 public double SamplesPerPulse => SampleRate*60.0/Bpm/Subdivision;
 double nextPulse;int tail,kind;
 public ClickTrack(int bpm,int beats,int subdivision,int sound,bool accent) {
  if(bpm<20||bpm>300||beats<1||beats>12||subdivision<1||subdivision>4||sound<0||sound>2)throw new ArgumentOutOfRangeException();
  Bpm=bpm;Beats=beats;Subdivision=subdivision;Sound=sound;Accent=accent;
 }
 public void Read(Span<float> mono) {
  for(int i=0;i<mono.Length;i++,Samples++) {
   if(Samples>=nextPulse) {
    kind=Pulses%(Beats*Subdivision)==0&&Accent?2:Pulses%Subdivision==0?1:0;
    tail=0;Pulses++;nextPulse=Pulses*SamplesPerPulse;
   }
   double t=tail++/(double)SampleRate;
   var frequency=(Sound==0?1500:Sound==1?850:1100)*(kind==2?1.5:kind==0?.72:1);
   var wave=Sound==2?Math.Sin(2*Math.PI*frequency*t)*Math.Sin(2*Math.PI*frequency*1.71*t):Math.Sin(2*Math.PI*frequency*t);
   mono[i]=t<.035?(float)(wave*Math.Exp(-t*(Sound==1?240:160))*(kind==2?.65:kind==1?.45:.22)):0;
  }
 }
}
public sealed class TapTempo {
 readonly Queue<double> taps=new();
 public int? Tap(double seconds) {
  if(taps.Count>0&&(seconds<=taps.Last()||seconds-taps.Last()>3))taps.Clear();
  taps.Enqueue(seconds);while(taps.Count>7)taps.Dequeue();
  if(taps.Count<2)return null;
  return Math.Clamp((int)Math.Round(60*(taps.Count-1)/(taps.Last()-taps.First())),20,300);
 }
}
