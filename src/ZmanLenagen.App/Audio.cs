using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using ZmanLenagen.Core;
namespace ZmanLenagen.App;
sealed class ClickProvider(ClickTrack track) : IWaveProvider {
 public WaveFormat WaveFormat {get;}=WaveFormat.CreateIeeeFloatWaveFormat(ClickTrack.SampleRate,1);
 public int Read(byte[] buffer,int offset,int count) {track.Read(MemoryMarshal.Cast<byte,float>(buffer.AsSpan(offset,count)));return count;}
}
sealed class MetronomeAudio : IDisposable {
 WasapiOut? output;
 public bool Playing=>output?.PlaybackState==PlaybackState.Playing;
 public void Start(int bpm,int beats,int subdivision,int sound,bool accent) {
  Stop();var player=new WasapiOut(AudioClientShareMode.Shared,true,40);
  try {player.Init(new ClickProvider(new ClickTrack(bpm,beats,subdivision,sound,accent)));player.Play();output=player;}
  catch {player.Dispose();throw;}
 }
 public void Stop(){var p=output;output=null;if(p!=null){p.Stop();p.Dispose();}}
 public void Dispose()=>Stop();
}
sealed class RecorderAudio : IDisposable {
 readonly RecordingArchive archive;
 WasapiCapture? capture;WaveFileWriter? writer;Recording? current;
 readonly object gate=new();
 WaveOutEvent? playback;AudioFileReader? reader;
 bool stopping;long flushBytes;Exception? captureError;
 public event Action<Recording?,Exception?>? Completed;
 public bool Recording=>capture!=null;
 public bool Stopping=>stopping;
 public string? CurrentId=>current?.Id;
 public string? PlayingId {get;private set;}
 public RecorderAudio(RecordingArchive archive){this.archive=archive;}
 public void Start(PracticeStep? step) {
  if(Recording)return;StopPlayback();
  var c=new WasapiCapture();
  var r=new Recording{StepId=step?.Id,Tag=step?.Tag??"הקלטה חופשית",Feedback=step?.Feedback,Notes=step?.Notes??""};
  try {
   archive.Save(r);var w=new WaveFileWriter(archive.Partial(r),c.WaveFormat);
   lock(gate){capture=c;writer=w;current=r;stopping=false;flushBytes=0;captureError=null;}
   c.DataAvailable+=Data;c.RecordingStopped+=Stopped;c.StartRecording();
  }catch {lock(gate){writer?.Dispose();writer=null;capture=null;current=null;}c.Dispose();throw;}
 }
 void Data(object? sender,WaveInEventArgs e) {
  lock(gate) {
   try {writer?.Write(e.Buffer,0,e.BytesRecorded);flushBytes+=e.BytesRecorded;
    if(writer!=null&&flushBytes>=writer.WaveFormat.AverageBytesPerSecond){writer.Flush();flushBytes=0;}
   }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){captureError=ex;if(!stopping){System.Threading.Tasks.Task.Run(Stop);}}
  }
 }
 public void Stop(){WasapiCapture? c;lock(gate){if(capture==null||stopping)return;stopping=true;c=capture;}c.StopRecording();}
 void Stopped(object? sender,StoppedEventArgs e) {
  Recording? r;Exception? error=e.Exception;
  lock(gate) {
   if(!ReferenceEquals(sender,capture))return;
   r=current;error??=captureError;
   try {writer?.Dispose();if(r!=null){r.Ended=DateTimeOffset.UtcNow;archive.Save(r);File.Move(archive.Partial(r),archive.Wav(r),true);}}
   catch(Exception ex){error=ex;}
   finally {writer=null;capture?.Dispose();capture=null;current=null;stopping=false;}
  }
  Completed?.Invoke(r,error);
 }
 public void Play(Recording r) {
  StopPlayback();var audio=new AudioFileReader(archive.Wav(r));var output=new WaveOutEvent();
  try {output.Init(audio);reader=audio;playback=output;PlayingId=r.Id;output.PlaybackStopped+=(s,e)=>{if(ReferenceEquals(output,playback))PlayingId=null;};output.Play();}
  catch{output.Dispose();audio.Dispose();throw;}
 }
 public void StopPlayback(){PlayingId=null;var p=playback;playback=null;if(p!=null){p.Stop();p.Dispose();}reader?.Dispose();reader=null;}
 public void Dispose() {
  StopPlayback();
  // Dispose joins the worker. Finalize synchronously because the WPF stop event
  // may be queued behind window shutdown. A later event is ignored by identity.
  Stop();var c=capture;c?.Dispose();if(c!=null)Stopped(c,new StoppedEventArgs(null));
 }
}
