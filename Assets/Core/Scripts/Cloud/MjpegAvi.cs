using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cevir.Core.Cloud
{
    /// <summary>
    /// Writes JPEG frames into a Motion-JPEG .avi file: no video encoder or plugin is needed on the phone, and the file
    /// plays in VLC and reads with ffmpeg / OpenCV (cv2.VideoCapture).
    /// </summary>
    public static class MjpegAvi
    {
        private const int AviIfHasIndex = 0x10;
        private const int AviIfKeyFrame = 0x10;

        public static byte[] Write(IList<byte[]> jpegFrames, int width, int height, float framesPerSecond)
        {
            int count = jpegFrames.Count;
            if (framesPerSecond <= 0f) framesPerSecond = 8f;
            int microSecPerFrame = (int)(1000000f / framesPerSecond);
            int rate = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(framesPerSecond * 1000f));
            int maxFrame = 0;
            foreach (byte[] frame in jpegFrames) if (frame.Length > maxFrame) maxFrame = frame.Length;

            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                Fourcc(w, "RIFF");
                long riffSize = Placeholder(w);
                Fourcc(w, "AVI ");

                // ---- headers
                Fourcc(w, "LIST");
                long hdrlSize = Placeholder(w);
                Fourcc(w, "hdrl");

                Fourcc(w, "avih");
                w.Write(56);
                w.Write(microSecPerFrame);
                w.Write((int)(maxFrame * framesPerSecond)); // max bytes per second
                w.Write(0);                                  // padding granularity
                w.Write(AviIfHasIndex);
                w.Write(count);
                w.Write(0);                                  // initial frames
                w.Write(1);                                  // streams
                w.Write(maxFrame);
                w.Write(width);
                w.Write(height);
                w.Write(0); w.Write(0); w.Write(0); w.Write(0);

                Fourcc(w, "LIST");
                long strlSize = Placeholder(w);
                Fourcc(w, "strl");

                Fourcc(w, "strh");
                w.Write(56);
                Fourcc(w, "vids");
                Fourcc(w, "MJPG");
                w.Write(0);              // flags
                w.Write((short)0);       // priority
                w.Write((short)0);       // language
                w.Write(0);              // initial frames
                w.Write(1000);           // scale
                w.Write(rate);           // rate (rate / scale = fps)
                w.Write(0);              // start
                w.Write(count);          // length
                w.Write(maxFrame);       // suggested buffer size
                w.Write(-1);             // quality
                w.Write(0);              // sample size
                w.Write((short)0); w.Write((short)0); w.Write((short)width); w.Write((short)height);

                Fourcc(w, "strf");
                w.Write(40);
                w.Write(40);             // BITMAPINFOHEADER size
                w.Write(width);
                w.Write(height);
                w.Write((short)1);       // planes
                w.Write((short)24);      // bits per pixel
                Fourcc(w, "MJPG");
                w.Write(width * height * 3);
                w.Write(0); w.Write(0); w.Write(0); w.Write(0);

                Fill(w, strlSize);
                Fill(w, hdrlSize);

                // ---- frames
                Fourcc(w, "LIST");
                long moviSize = Placeholder(w);
                long moviStart = stream.Position;
                Fourcc(w, "movi");
                var offsets = new int[count];
                for (int i = 0; i < count; i++)
                {
                    byte[] frame = jpegFrames[i];
                    offsets[i] = (int)(stream.Position - moviStart);
                    Fourcc(w, "00dc");
                    w.Write(frame.Length);
                    w.Write(frame);
                    if ((frame.Length & 1) == 1) w.Write((byte)0);
                }

                Fill(w, moviSize);

                // ---- index
                Fourcc(w, "idx1");
                w.Write(count * 16);
                for (int i = 0; i < count; i++)
                {
                    Fourcc(w, "00dc");
                    w.Write(AviIfKeyFrame);
                    w.Write(offsets[i]);
                    w.Write(jpegFrames[i].Length);
                }

                Fill(w, riffSize);
                w.Flush();
                return stream.ToArray();
            }
        }

        private static void Fourcc(BinaryWriter w, string code)
        {
            w.Write(Encoding.ASCII.GetBytes(code));
        }

        private static long Placeholder(BinaryWriter w)
        {
            long position = w.BaseStream.Position;
            w.Write(0);
            return position;
        }

        /// <summary>Writes the size of the chunk whose size field is at <paramref name="sizeField"/>.</summary>
        private static void Fill(BinaryWriter w, long sizeField)
        {
            long end = w.BaseStream.Position;
            w.BaseStream.Position = sizeField;
            w.Write((int)(end - sizeField - 4));
            w.BaseStream.Position = end;
        }
    }
}
