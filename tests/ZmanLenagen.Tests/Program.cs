using ZmanLenagen.Core;
int count=0;
void Equal<T>(T expected,T actual) {count++;if(!Equals(expected,actual))throw new Exception($"Test {count}: expected {expected}, got {actual}");}
Equal("15",Metrics.Timer(900));Equal("2",Metrics.Timer(61));Equal("1",Metrics.Timer(60));Equal("<1",Metrics.Timer(59));Equal("0",Metrics.Timer(0));Equal("−<1",Metrics.Timer(-1));Equal("−1",Metrics.Timer(-60));
var start=new DateTimeOffset(2026,10,5,23,50,0,TimeSpan.Zero);
var step=new PracticeStep{PlannedMinutes=15,Tag="test",Started=start,Segments=[new(start,start.AddMinutes(22))],Feedback="half"};
Equal(1320d,step.MeasuredSeconds);Equal(660d,step.CreditedSeconds);
var daily=Metrics.Daily([step],TimeZoneInfo.Utc);Equal(300d,daily[new DateOnly(2026,10,5)]);Equal(360d,daily[new DateOnly(2026,10,6)]);
step.Feedback="no";Equal(0d,step.CreditedSeconds);Equal(0,Metrics.Daily([step],TimeZoneInfo.Utc).Count);
step.Feedback="yes";step.Segments.Add(new(start.AddHours(1),start.AddHours(1).AddMinutes(2)));Equal(1440d,step.CreditedSeconds);
step.Feedback=null;Equal(0d,step.CreditedSeconds);
Metrics.ValidateCircuit([new("סולמות",10)]);
try{Metrics.ValidateCircuit([new("bad",0)]);throw new Exception("Invalid minutes accepted");}catch(ArgumentException){count++;}
try{Metrics.ValidateCircuit([]);throw new Exception("Empty accepted");}catch(ArgumentException){count++;}
// DST day length must preserve measured total.
var zone=TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows()?"Israel Standard Time":"Asia/Jerusalem");
step.Feedback="yes";step.Segments=[new(new DateTimeOffset(2026,10,24,20,0,0,TimeSpan.Zero),new DateTimeOffset(2026,10,25,5,0,0,TimeSpan.Zero))];
Equal(step.CreditedSeconds,Metrics.Daily([step],zone).Values.Sum());


Equal(12,Fifths.Keys.Count);Equal(1,Fifths.Keys.Count(k=>k.Major.Contains('/')));Equal("E♭m / D♯m",Fifths.Keys[6].Minor);
var taps=new TapTempo();Equal<int?>(null,taps.Tap(0));Equal<int?>(120,taps.Tap(.5));Equal<int?>(120,taps.Tap(1));Equal<int?>(null,taps.Tap(5));Equal<int?>(60,taps.Tap(6));
// Ten minutes at 137 BPM with triplets: awkward fractional periods must not drift.
var track=new ClickTrack(137,4,3,0,true);var block=new float[48000];
for(int i=0;i<600;i++)track.Read(block);
Equal(28800000L,track.Samples);Equal(4110L,track.Pulses);
// Callback buffer boundaries must not alter the audio stream.
var a=new ClickTrack(123,6,2,1,true);var b=new ClickTrack(123,6,2,1,true);
var large=new float[24000];var pieces=new float[24000];a.Read(large);
for(int i=0;i<pieces.Length;i+=137)b.Read(pieces.AsSpan(i,Math.Min(137,pieces.Length-i)));
Equal(true,large.SequenceEqual(pieces));
Console.WriteLine($"Passed {count} assertions.");
