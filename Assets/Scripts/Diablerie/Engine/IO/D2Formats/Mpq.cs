using StormLib;

namespace Diablerie.Engine.IO.D2Formats
{
    public static class Mpq
    {
        public static MpqFileSystem fs = new MpqFileSystem();

        public static void AddArchive(string filename, bool optional = false)
        {
            try
            {
                IMpqArchive archive = null;
                if (filename.EndsWith(".zip", System.StringComparison.OrdinalIgnoreCase))
                {
                    archive = new ZipArchive(filename);
                }
                else
                {
                    archive = new MpqArchive(filename);
                }
                
                fs.Archives.Add(archive);
            }
            catch (System.IO.FileNotFoundException)
            {
                if (!optional)
                    throw;
            }
        }

        public static byte[] ReadAllBytes(string filename)
        {
            UnityEngine.Profiling.Profiler.BeginSample("Mpq.ReadAllBytes");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using (var stream = fs.OpenFile(filename))
                {
                    byte[] bytes = stream.ReadAllBytes();
                    UnityEngine.Debug.Log("Mpq.ReadAllBytes " + filename + " " + sw.ElapsedMilliseconds + " ms");
                    return bytes;
                }
            }
            finally
            {
                UnityEngine.Profiling.Profiler.EndSample();
            }
        }

        public static unsafe string ReadAllText(string filename)
        {
            UnityEngine.Profiling.Profiler.BeginSample("Mpq.ReadAllText");
            try
            {
                using (var stream = fs.OpenFile(filename))
                {
                    byte[] bytes = stream.ReadAllBytes();
                    string result;
                    fixed (byte* pointer = bytes)
                    {
                        result = new string((sbyte*) pointer);
                    }
                    return result;
                }
            }
            finally
            {
                UnityEngine.Profiling.Profiler.EndSample();
            }
        }
    }
}
