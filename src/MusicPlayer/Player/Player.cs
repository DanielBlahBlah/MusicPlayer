using System.IO;
using System.Windows.Threading;
using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;
using MusicPlayer.LibraryObjects;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MusicPlayer.Player;

public sealed class AudioPlayer : IDisposable
{

    static AudioPlayer()
    {
        DynamicallyLoadedBindings.LibrariesPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "win-x64");
        DynamicallyLoadedBindings.Initialize();
    }

    private const int baseRate = 44100;
    private const int maxRate = 384000;

    private sealed class Entry(Decoder decoder, int index, float gain, int rate)
    {
        public Decoder AudioDecoder { get;} = decoder;
        public int Index { get;} = index;
        public float Gain { get;} = gain;
        public int Rate { get;} = rate;
    }

    public sealed class Feed(AudioPlayer owner, int rate) : ISampleProvider
    {
        public WaveFormat WaveFormat { get;} = WaveFormat.CreateIeeeFloatWaveFormat(rate, Decoder.Channels);
        public int Read(float[] buffer, int offset, int count) => owner.ReadAudio(buffer, offset, count);
    }

    private readonly Dispatcher ui = Dispatcher.CurrentDispatcher;
    private readonly object gate = new();

    private Entry? playing;
    private Entry? upNext;
    private WaveOutEvent? output;
    private volatile int playingIndex = -1;

    private volatile int outputRate;
    private volatile bool switching;

    public Queue queue { get;} = new();

    public Track? current => playingIndex >= 0 && playingIndex < queue.tracks.Count ? queue.tracks[playingIndex] : null;
    public bool isActive => playingIndex >= 0;

    public event Action? StateChanged;

    private static int NativeRate(Track t) => t.sampleRate is > 0 and var r ? Math.Min(r, maxRate) : baseRate;

    public void Enqueue(IEnumerable<Track> items)
    {
        var firstNew = queue.tracks.Count;
        queue.Enqueue(items);

        if (queue.tracks.Count <= firstNew) return;
        if(!isActive) PlayAt(firstNew);
        else PrepareNext();
    }

    public void PlayAt(int index)
    {
        if (output?.PlaybackState != PlaybackState.Playing) outputRate = 0;

        var entry = OpenFrom(index);

        if (entry == null)
        {
            Stop();
            return;
        }

        lock (gate)
        {
            playing?.AudioDecoder.Dispose();
            upNext?.AudioDecoder.Dispose();
            playing = entry;
            upNext = null;
            playingIndex = entry.Index;
            switching = false;
        }

        if (entry.Rate != outputRate)
        {
            outputRate = entry.Rate;
            DisposeOutput();
        }

        StartOutput();
        StateChanged?.Invoke();
        PrepareNext();
    }

    public void Next()
    {
        var i = playingIndex;
        if (i >= 0) PlayAt(i + 1);
    }

    public void Stop()
    {
        lock (gate)
        {
            playing?.AudioDecoder.Dispose();
            upNext?.AudioDecoder.Dispose();
            playing = null;
            upNext = null;
            playingIndex = -1;
            switching = false;
        }

        DisposeOutput();
        StateChanged?.Invoke();
    }

    public void Dispose() => Stop();

    private int ReadAudio(float[] buffer, int offset, int count)
    {
        if (switching) return 0;

        lock (gate)
        {
            var total = 0;

            while (total < count && playing != null)
            {
                var n = playing.AudioDecoder.Read(buffer, offset + total, count - total);

                if (n > 0)
                {
                    var gain = playing.Gain;
                    if (gain != 1f)
                    {
                        for (var i = 0; i < n; i++)
                        {
                            var j = offset + total + i;
                            buffer[j] = Math.Clamp(buffer[j] * gain, -1f, 1f);
                        }
                    }

                    total += n;
                }

                if (total < count)
                {
                    playing.AudioDecoder.Dispose();
                    playing = upNext;
                    upNext = null;

                    if (playing != null)
                    {
                        playingIndex = playing.Index;
                        ui.BeginInvoke(OnAdvanced);

                        if (playing.Rate != outputRate)
                        {
                            outputRate = playing.Rate;
                            switching = true;
                            return total;
                        }
                    }
                }
            }

            return total;
        }
    }

    private void OnAdvanced()
    {
        StateChanged?.Invoke();
        PrepareNext();
    }

    private void PrepareNext()
    {
        int index;

        lock (gate)
        {
            if (playing == null || upNext != null) return;
            index = playing.Index + 1;
        }

        var entry = OpenFrom(index);
        if (entry == null) return;

        lock (gate)
        {
            if (playing != null && upNext == null && entry.Index > playing.Index) upNext = entry;
            else entry.AudioDecoder.Dispose();
        }
    }

    private Entry? OpenFrom(int index)
    {
        for (; index >= 0 && index < queue.tracks.Count; index++)
        {
            var track = queue.tracks[index];

            try
            {
                var rate = Math.Max(outputRate, NativeRate(track));
                return new Entry(new Decoder(track.filePath, rate), index, GainFor(track), rate);
            }
            catch (Exception)
            {

            }
        }

        return null;
    }

    private static float GainFor(Track track)
    {
        if (track.replayGainTrackGain is not {} db) return 1f;

        var gain = (float)Math.Pow(10, db/20.0);

        if (track.replayGainTrackPeak is {} peak && peak > 0) gain = Math.Min(gain, (float)(1.0/peak));

        return gain;
    }

    private void StartOutput()
    {
        if (output?.PlaybackState == PlaybackState.Playing) return;

        DisposeOutput();

        output = new WaveOutEvent { DesiredLatency = 200};
        output.Init(new SampleToWaveProvider(new Feed(this, outputRate)));
        output.PlaybackStopped += OnPlaybackStopped;
        output.Play();
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (sender != output) return;

        if (e.Exception != null)
        {
            System.Windows.MessageBox.Show("Playback error:\n\n" + e.Exception);
            Stop();
            return;
        }

        bool restart;

        lock (gate)
        {
            restart = playing != null;
            if (!restart) playingIndex = -1;
            switching = false;
        }

        if (restart) StartOutput();
        else StateChanged?.Invoke();
    }

    private void DisposeOutput()
    {
        var old = output;
        output = null;
        if (old == null) return;

        old.PlaybackStopped -= OnPlaybackStopped;
        old.Stop();
        old.Dispose();
    }
}