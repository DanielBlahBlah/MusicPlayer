using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Abstractions;

namespace MusicPlayer.Player;

public sealed unsafe class Decoder : IDisposable
{
    public const int Channels = 2;

    private AVFormatContext* format;
    private AVCodecContext* codec;
    private SwrContext* swr;
    private AVPacket* packet;
    private AVFrame* frame;
    private readonly int streamIndex;
    private readonly int outputRate;

    private float[] pcm = new float[16384];
    private int pcmPos, pcmCount;
    private bool inputDone, drained;

    public Decoder(string path, int outputRate)
    {
        this.outputRate = outputRate;

        try
        {
            AVFormatContext* f = null;
            Check(ffmpeg.avformat_open_input(&f, path, null, null));
            format = f;
            Check(ffmpeg.avformat_find_stream_info(format,null));

            AVCodec* decoder = null;
            streamIndex = Check(ffmpeg.av_find_best_stream(format, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, &decoder, 0));

            codec = ffmpeg.avcodec_alloc_context3(decoder);
            Check(ffmpeg.avcodec_parameters_to_context(codec, format->streams[streamIndex]->codecpar));
            Check(ffmpeg.avcodec_open2(codec,decoder,null));

            if (codec -> ch_layout.order == AVChannelOrder.AV_CHANNEL_ORDER_UNSPEC) ffmpeg.av_channel_layout_default(&codec->ch_layout, codec->ch_layout.nb_channels);

            var outLayout = new AVChannelLayout();
            ffmpeg.av_channel_layout_default(&outLayout, Channels);

            SwrContext* s = null;
            Check(ffmpeg.swr_alloc_set_opts2(&s, &outLayout, AVSampleFormat.AV_SAMPLE_FMT_FLT, outputRate, &codec->ch_layout, codec->sample_fmt, codec->sample_rate,0,null));
            swr = s;
            Check(ffmpeg.swr_init(swr));

            packet = ffmpeg.av_packet_alloc();
            frame = ffmpeg.av_frame_alloc();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var written = 0;

        while (written < count)
        {
            if (pcmPos >= pcmCount)
            {
                if (!DecodeMore()) break;
            }

            var n = Math.Min(count - written, pcmCount - pcmPos);

            for (var i = 0; i < n; i++) buffer[offset + written + i] = pcm[pcmPos + i];

            pcmPos += n;
            written += n;
        }

        return written;
    }

    private bool DecodeMore()
    {
        pcmPos = pcmCount = 0;

        while (true)
        {
            var r = ffmpeg.avcodec_receive_frame(codec,frame);

            if (r == 0)
            {
                ConvertFrame(frame);
                ffmpeg.av_frame_unref(frame);
                if (pcmCount > 0) return true;
                continue;
            }

            if (r == ffmpeg.AVERROR_EOF)
            {
                if (drained) return false;
                drained = true;
                ConvertFrame(null);
                return pcmCount > 0;
            }

            if (r != -11) return false;

            if (inputDone) return false;

            var read = ffmpeg.av_read_frame(format, packet);
            if (read < 0)
            {
                inputDone = true;
                ffmpeg.avcodec_send_packet(codec, null);
                continue;
            }

            if (packet -> stream_index == streamIndex) ffmpeg.avcodec_send_packet(codec, packet);
            ffmpeg.av_packet_unref(packet);
        }
    }

    private void ConvertFrame(AVFrame* f)
    {
        var inSamples = f == null ? 0 : f->nb_samples;
        var outSamples = (int)ffmpeg.av_rescale_rnd(
            ffmpeg.swr_get_delay(swr,codec->sample_rate) + inSamples,
            outputRate, codec->sample_rate, AVRounding.AV_ROUND_UP) + 256;

        if (pcm.Length < outSamples * Channels) pcm = new float[outSamples * Channels];

        int got;
        fixed (float* p = pcm)
        {
            var o = (byte*)p;
            got = ffmpeg.swr_convert(swr, &o, outSamples, f == null ? null : f->extended_data, inSamples);
        }

        pcmPos = 0;
        pcmCount = got > 0 ? got * Channels : 0;
    }

    private static int Check(int error)
    {
        if (error >= 0) return error;

        var buffer = stackalloc byte[1024];
        ffmpeg.av_strerror(error, buffer, 1024);
        throw new InvalidOperationException($"FFmpeg: {Marshal.PtrToStringAnsi((IntPtr)buffer)}({error})");
    }

    public void Dispose()
    {
        var f = frame; if (f != null) ffmpeg.av_frame_free(&f); frame = null;
        var p = packet; if (p != null) ffmpeg.av_packet_free(&p); packet = null;
        var s = swr; if (s != null) ffmpeg.swr_free(&s); swr = null;
        var c = codec; if (c != null) ffmpeg.avcodec_free_context(&c); codec = null;
        var fmt = format; if (fmt != null) ffmpeg.avformat_close_input(&fmt); format = null;
    }
}