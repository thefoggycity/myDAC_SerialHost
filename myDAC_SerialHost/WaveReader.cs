using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace myDAC_SerialHost
{
    class WaveReader
    {
        private FileStream WaveStream;
        private Info WaveInfo;

        public bool IsWave;

        public struct Info
        {
            public UInt16 AudioFormat;
            public UInt16 Channels;
            public UInt32 SampleRate;
            public UInt32 ByteRate;
            public UInt16 BlockAlign;
            public UInt16 BitPerSample;
            public UInt32 FmtOffset;
            public UInt32 DataOffset;
            public UInt32 DataLength;
        }

        public WaveReader(FileInfo Wavefile)
        {
            WaveStream = Wavefile.OpenRead();
            WaveInfo = new Info();
            AnalyseFile();
        }
        public WaveReader(FileStream Wavefile)
        {
            WaveStream = Wavefile;
            WaveInfo = new Info();
            AnalyseFile();
        }

        public byte[] GetSample()
        {
            if (DetectEnd()) return null;
            WaveStream.Seek(WaveInfo.BlockAlign - ((WaveStream.Position - 
                WaveInfo.DataOffset) % WaveInfo.BlockAlign), SeekOrigin.Current);
            byte[] Sample = new byte[WaveInfo.BitPerSample >> 3];
            WaveStream.Read(Sample, 0, WaveInfo.BitPerSample >> 3);
            return Sample;
        }

        public byte[] GetSampleAt(int FrameIndex)
        {
            byte[] Sample = new byte[WaveInfo.BitPerSample >> 3];
            WaveStream.Position = FrameIndex * WaveInfo.BlockAlign + WaveInfo.DataOffset;
            WaveStream.Read(Sample, 0, WaveInfo.BitPerSample >> 3);
            return Sample;
        }

        public byte[] GetFrame()
        {
            if (DetectEnd()) return null;
            WaveStream.Seek(WaveInfo.BlockAlign - ((WaveStream.Position -
                WaveInfo.DataOffset) % WaveInfo.BlockAlign), SeekOrigin.Current);
            byte[] Frame = new byte[(WaveInfo.BitPerSample >> 3) * WaveInfo.Channels];
            WaveStream.Read(Frame, 0, (WaveInfo.BitPerSample >> 3) * WaveInfo.Channels);
            return Frame;
        }

        public byte[] GetFrameAt(int FrameIndex)
        {
            byte[] Frame = new byte[(WaveInfo.BitPerSample >> 3) * WaveInfo.Channels];
            WaveStream.Position = FrameIndex * WaveInfo.BlockAlign + WaveInfo.DataOffset;
            WaveStream.Read(Frame, 0, (WaveInfo.BitPerSample >> 3) * WaveInfo.Channels);
            return Frame;
        }

        public void SkipFrame(int Frames)
        {
            WaveStream.Seek(WaveInfo.BlockAlign - ((WaveStream.Position - WaveInfo.DataOffset) %
                WaveInfo.BlockAlign) + Frames * WaveInfo.BlockAlign, SeekOrigin.Current);
        }

        public int GetFrameIndex()
        {
            return (int)((WaveStream.Position - WaveInfo.DataOffset) / WaveInfo.BlockAlign);
        }

        public int GetTotalFrame()
        {
            return (int)(WaveInfo.DataLength / WaveInfo.BlockAlign);
        }

        public bool DetectEnd()
        {
            if ((WaveInfo.DataLength - WaveStream.Position) < WaveInfo.BlockAlign) return true;
            else return false;
        }

        public Info GetInfo()
        {
            return WaveInfo;
        }

        public void Close()
        {
            IsWave = false;
            WaveStream.Dispose();
        }

        // WARNING: THE FILE WILL NOT BE ANALYSED CORRECTLY IF FOURCCS ARE NOT AT 4N POSITIONS
        private void AnalyseFile()
        {
            byte[] RIFF = { 0x52, 0x49, 0x46, 0x46 };
            byte[] WAVE = { 0x57, 0x41, 0x56, 0x45 };
            byte[] FMT_ = { 0x66, 0x6d, 0x74, 0x20 };   // In lower case
            byte[] DATA = { 0x64, 0x61, 0x74, 0x61 };   // In lower case

            byte[] ReadWindow = new byte[4];

            WaveStream.Read(ReadWindow, 0, 4);
            if (ByteArrayCompare(ReadWindow, RIFF))
            {
                WaveStream.Position = 8;
                WaveStream.Read(ReadWindow, 0, 4);
                if (!ByteArrayCompare(ReadWindow, WAVE))
                {
                    IsWave = false;
                    return;
                }
            }
            else
            {
                IsWave = false;
                return;
            }
            IsWave = true;

            // It is not likely that "fmt_" is not at 4n position.
            do WaveStream.Read(ReadWindow, 0, 4);
            while (!ByteArrayCompare(ReadWindow, FMT_));
            WaveStream.Seek(4, SeekOrigin.Current);     // Skip the fmtSize
            WaveInfo.FmtOffset = (uint)WaveStream.Position;

            WaveStream.Read(ReadWindow, 0, 4);
            WaveInfo.AudioFormat = (ushort)(ReadWindow[0] + 0x100 * ReadWindow[1]);
            WaveInfo.Channels = (ushort)(ReadWindow[2] + 0x100 * ReadWindow[3]);

            WaveStream.Read(ReadWindow, 0, 4);
            WaveInfo.SampleRate = (uint)(ReadWindow[0] + 0x100 * ReadWindow[1] + 
                                    0x10000 * ReadWindow[2] + 0x1000000 * ReadWindow[3]);

            WaveStream.Read(ReadWindow, 0, 4);
            WaveInfo.ByteRate = (uint)(ReadWindow[0] + 0x100 * ReadWindow[1] +
                                    0x10000 * ReadWindow[2] + 0x1000000 * ReadWindow[3]);

            WaveStream.Read(ReadWindow, 0, 4);
            WaveInfo.BlockAlign = (ushort)(ReadWindow[0] + 0x100 * ReadWindow[1]);
            WaveInfo.BitPerSample = (ushort)(ReadWindow[2] + 0x100 * ReadWindow[3]);

            // It is highly possible that "DATA" is not at 4n position, thus check each position.
            do
            {
                WaveStream.Read(ReadWindow, 0, 4);
                WaveStream.Seek(-3, SeekOrigin.Current);
            }
            while (!ByteArrayCompare(ReadWindow, DATA));
            WaveStream.Read(ReadWindow, 0, 4);
            WaveInfo.DataLength = (uint)(ReadWindow[0] + 0x100 * ReadWindow[1] +
                                    0x10000 * ReadWindow[2] + 0x1000000 * ReadWindow[3]);
            WaveInfo.DataOffset = (uint)WaveStream.Position;

            return;
        }

        private bool ByteArrayCompare(byte[] A, byte[] B)
        {
            if (A.Count() != B.Count()) return false;
            else
                for (int i = 0; i < A.Count(); i++)
                    if (A[i] != B[i]) return false;
            return true;
        }
    }
}
