using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace StormLib
{
    public interface IMpqArchive : IDisposable
    {
        bool HasFile(string filename);
        MpqFileStreamBase OpenFile(string filename);
        void Close();
    }

    class ZipLocalFileHeader
    {
        public uint signature;
        public ushort version;
        public ushort flags;
        public ushort compression;
        public ushort modifyTime;
        public ushort modifyDate;
        public uint crc32;
        public uint compressedSize;
        public uint uncompressedSize;
        public ushort fileNameLength;
        public ushort extraFieldLength;
        public byte[] fileName;
        public byte[] extraField;


        public long fileDataOffset;

        public bool ContainsDataDescriptor
        {
            get
            {
                long dataDescriptorFlag = (flags >> 3) & 1u;
                return dataDescriptorFlag != 0;
            }
        }

        public string Date
        {
            get
            {
                int day = modifyDate & 0b11111;
                int month = (modifyDate >> 5) & 0b1111;
                int year = (modifyDate >> 9) & 0b1111111;
                year = year + 1980;
                return string.Format("{0:d4}-{1:d2}-{2:d2}", year, month, day);
            }
        }

        public string Time
        {
            get
            {
                int second = (modifyTime & 0b11111) * 2;
                int minute = (modifyTime >> 5) & 0b111111;
                int hour = (modifyTime >> 11) & 0b11111;
                return string.Format("{0:d2}:{1:d2}:{2:d2}", hour, minute, second);
            }
        }
    }

    [Serializable]
    internal class BadZipArchiveFileFormatException : Exception
    {
        public BadZipArchiveFileFormatException()
        {
        }

        public BadZipArchiveFileFormatException(string message) : base(message)
        {
        }

        public BadZipArchiveFileFormatException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected BadZipArchiveFileFormatException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    class ZipArchiveReaderSettings
    {
        public bool debug = false;
        public bool useListFile = true;
    }

    class ZipArchiveReader
    {
        FileStream zipFileStream;
        ZipArchiveReaderSettings settings;
        string m_filePath;

        byte[] buffer;
        public ZipArchiveReader(string filePath)
        {
            settings = new ZipArchiveReaderSettings();
            zipFileStream = File.OpenRead(filePath);
            m_filePath = filePath;
            buffer = new byte[8];
        }

        uint ReadUint()
        {
            if (zipFileStream.Length - zipFileStream.Position < 4)
            {
                throw new BadZipArchiveFileFormatException();
            }

            zipFileStream.Read(buffer, 0, 4);
            return BitConverter.ToUInt32(buffer, 0);
        }

        ushort ReadUshort()
        {
            if (zipFileStream.Length - zipFileStream.Position < 4)
            {
                throw new BadZipArchiveFileFormatException();
            }

            zipFileStream.Read(buffer, 0, 2);
            return BitConverter.ToUInt16(buffer, 0);
        }

        byte[] ReadBytes(int length)
        {
            if (zipFileStream.Length - zipFileStream.Position < length)
            {
                throw new BadZipArchiveFileFormatException();
            }
            byte[] result = new byte[length];
            zipFileStream.Read(result, 0, length);
            return result;
        }

        void SkipBytes(long length)
        {
            if (zipFileStream.Length - zipFileStream.Position < length)
            {
                throw new BadZipArchiveFileFormatException();
            }
            zipFileStream.Seek(length, SeekOrigin.Current);
        }

        public FileStream GetStream()
        {
            return zipFileStream;
        }

        public void Close()
        {
            if (zipFileStream != null)
            {
                zipFileStream.Close();
                zipFileStream = null;
            }
        }

        Dictionary<string, ZipLocalFileHeader> GetFileListByListFile()
        {
            string listFilename = m_filePath + ".list";
            if (!File.Exists(listFilename))
            {
                throw new FileNotFoundException(listFilename + " not found.", listFilename);
            }

            Dictionary<string, ZipLocalFileHeader> fileList = new Dictionary<string, ZipLocalFileHeader>();
            using (StreamReader reader = new StreamReader(listFilename))
            {
                // skip first 2 lines
                reader.ReadLine();
                reader.ReadLine();
                string line = null;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("total file count:") ||
                        line.StartsWith("End list for zip archive")
                        )
                    {
                        break;
                    }

                    string[] splits = line.Split(',');
                    if (splits.Length != 3)
                    {
                        continue;
                    }

                    ZipLocalFileHeader lfh = new ZipLocalFileHeader();
                    string name = splits[0];
                    lfh.fileDataOffset = long.Parse(splits[1]);
                    lfh.compressedSize = uint.Parse(splits[2]);

                    fileList.Add(name, lfh);
                }
            }
            return fileList;
        }

        Dictionary<string, ZipLocalFileHeader> GetFileListByScanning()
        {
            Dictionary<string, ZipLocalFileHeader> fileList = new Dictionary<string, ZipLocalFileHeader>();
            uint signature;
            int fileCount = 0;
            int dirCount = 0;
            while ((signature = ReadUint()) == 0x04034b50u)
            {
                ZipLocalFileHeader lfh = new ZipLocalFileHeader();
                lfh.signature = signature;
                lfh.version = ReadUshort();
                lfh.flags = ReadUshort();
                lfh.compression = ReadUshort();
                lfh.modifyTime = ReadUshort();
                lfh.modifyDate = ReadUshort();
                lfh.crc32 = ReadUint();
                lfh.compressedSize = ReadUint();
                lfh.uncompressedSize = ReadUint();
                lfh.fileNameLength = ReadUshort();
                lfh.extraFieldLength = ReadUshort();
                lfh.fileName = ReadBytes(lfh.fileNameLength);
                lfh.extraField = ReadBytes(lfh.extraFieldLength);
                lfh.fileDataOffset = zipFileStream.Position;

                byte[] fileData = null;
                if (settings.debug)
                {
                    fileData = ReadBytes((int)lfh.compressedSize);
                }
                else
                {
                    SkipBytes(lfh.compressedSize);
                }

                if (lfh.fileName[lfh.fileNameLength - 1] == '/')
                {
                    dirCount++;
                }
                else
                {
                    fileCount++;
                    string key = Encoding.UTF8.GetString(lfh.fileName).ToLower();
                    fileList.Add(key, lfh);
                }
            }

            return fileList;
        }

        public Dictionary<string, ZipLocalFileHeader> GetFileList()
        {
            if (settings.useListFile)
            {
                return GetFileListByListFile();
            }
            else
            {
                return GetFileListByScanning();
            }
        }
    }

    public class ZipArchive : IMpqArchive
    {
        ZipArchiveReader m_reader;
        Dictionary<string, ZipLocalFileHeader> m_fileList;
        string m_archiveFilename;

        public ZipArchive(string filename)
        {
            if (!File.Exists(filename))
            {
                throw new FileNotFoundException(filename);
            }
            m_reader = new ZipArchiveReader(filename);
            m_fileList = m_reader.GetFileList();
            m_archiveFilename = filename;
        }

        public void Close()
        {
            if (m_reader != null)
            {
                m_reader.Close();
                m_reader = null;
            }
        }

        public void Dispose()
        {
            Close();
        }

        public bool HasFile(string filename)
        {
            filename = NormalizeFilename(filename);
            return m_fileList.ContainsKey(filename);
        }

        private string NormalizeFilename(string filename)
        {
            return filename.Replace("\\", "/").ToLower();
        }

        public MpqFileStreamBase OpenFile(string filename)
        {
            filename = NormalizeFilename(filename);
            if (!m_fileList.ContainsKey(filename))
            {
                throw new FileNotFoundException();
            }
            
            var header = m_fileList[filename];
            long offset = header.fileDataOffset;
            long length = header.compressedSize;
            return new ZipFileStream(m_reader.GetStream(), offset, length);
        }
    }

    public class MpqArchive : IMpqArchive
    {
        IntPtr handle = IntPtr.Zero;

        public MpqArchive(string filename)
        {
            if (!File.Exists(filename))
                throw new FileNotFoundException(filename);
            if (!StormLib.SFileOpenArchive(filename, 0, OpenArchiveFlags.READ_ONLY, out handle))
                throw new IOException("SFileOpenArchive failed");
        }

        public bool HasFile(string filename)
        {
            return StormLib.SFileHasFile(handle, filename);
        }

        public MpqFileStreamBase OpenFile(string filename)
        {
            if (!HasFile(filename))
                throw new FileNotFoundException();

            IntPtr fileHandle;
            if (!StormLib.SFileOpenFileEx(handle, filename, OpenFileFlags.FROM_MPQ, out fileHandle))
                throw new IOException("SFileOpenFileEx failed");

            return new MpqFileStream(fileHandle);
        }

        public void Close()
        {
            if (handle != IntPtr.Zero)
            {
                StormLib.SFileCloseArchive(handle);
                handle = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
