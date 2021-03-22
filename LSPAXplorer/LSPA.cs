using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// http://wiki.xentax.com/index.php/DGTEFF

namespace LucasDuff
{
	class LSPA
	{
		private static class Utility
		{
			public static byte[] Read(FileStream Reader, int length)
			{
				byte[] output = new byte[length];
				Reader.Read(output, 0, length);
				return output;
			}

			public static byte[] GetAllBytesBeforeNull(byte[] b)
			{
				byte[] bBeforeNull = new byte[8];
				int j = 0;
				do
				{
					bBeforeNull[j] = b[j];
				} while (b[j++] != 0);
				return bBeforeNull;
			}

			public static DateTime GetModifiedDate(byte[] dateBytes)
			{
				// Get date from modifed date bytes
				short year = Byte2ToInt(dateBytes);
				byte month = dateBytes[2];
				byte day = dateBytes[3];
				byte hours = dateBytes[4];
				byte minute = dateBytes[5];
				byte second = dateBytes[6];
				DateTime modifiedDate = new DateTime(year, month, day, hours, minute, second);

				return modifiedDate;
			}


			public static short Byte2ToInt(byte[] bytes, int offset = 0)
			{
				return BitConverter.ToInt16(bytes, offset);
			}

			public static int Byte4ToInt(byte[] bytes, int offset = 0)
			{
				return BitConverter.ToInt32(bytes, offset);
			}

			public static long Byte8ToInt(byte[] bytes, int offset = 0)
			{
				return BitConverter.ToInt64(bytes, offset);
			}
		}

		public class Chunk
		{
			public struct Node
			{
				public Flags Flag;
				public string Name;
				public byte[] Metadata;
				public long Offset;
				public long Size;
				public List<Chunk.Node> Children;
			}

			public static void AddChild(Chunk.Node self, Chunk.Node child)
			{
				self.Children.Add(child);
			}
		}

		// All flags of LSPA entries
		public struct Flags
		{
			public bool Hidden;
			public bool Metadata;
			public bool ReadOnly;
			public bool Archive;
			public bool System;
		}

		static Flags GetFlags (short header)
		{
			// Create 5 bit binary from header
			string binary = Convert.ToString(header, 2);
			binary = Regex.Replace(binary, "[0-9]+", match => match.Value.PadLeft(5, '0'));

			// Calculate flags from binary
			Flags flags = new Flags();
			flags.System = binary[0] == '1';
			flags.Archive = binary[1] == '1';
			flags.ReadOnly = binary[2] == '1';
			flags.Metadata = binary[3] == '1';
			flags.Hidden = binary[4] == '1';
			
			return flags;
		}

		// Method to verify LMLM file
		static bool VerifyLMLM(byte[] magic)
		{
			return System.Text.Encoding.UTF8.GetString(magic) == "LSPA";
		}

		// Method to process directory in LSPA entries (Recursive)
		static Chunk.Node DirectoryGetEntries(FileStream Reader, Chunk.Node parent, long numOfEntries)
		{
			for (int i = 0; i < numOfEntries; i++)
			{
				long chunkStartIndex = Reader.Position;     // Keep original offset for allignment
				Chunk.Node currentChunk = new Chunk.Node(); // Create new chunk node for entry

				// Get entry flags and entry name
				short header = Utility.Byte2ToInt(Utility.Read(Reader, 2));
				Flags flags = GetFlags(header);
				string fileName = System.Text.UnicodeEncoding.Unicode.GetString(Utility.Read(Reader, dataChunkLength)).TrimEnd('\0');

				// Get metadata if flag
				byte[] metadata = null;
				if (flags.Metadata)
				{
					metadata = Utility.Read(Reader, 10);
				}

				// Read entry size and offset
				byte[] fileSizeB = Utility.Read(Reader, 8);
				long fileSize;
				long fileOffset = Utility.Byte8ToInt(Utility.Read(Reader, 8));

				
				if (fileOffset == 0)	// If directory
				{
					currentChunk.Children = new List<Chunk.Node>();

					fileSize = Utility.Byte8ToInt(Utility.GetAllBytesBeforeNull(fileSizeB));

					Reader.Position = chunkStartIndex += dirChunkLength; // Allign
					if (fileSize != 0)
					{
						currentChunk = DirectoryGetEntries(Reader, currentChunk, fileSize); // Get entries in directory
					}
					else
					{
						Reader.Position += dataChunkLength; // Allign
					}

				}
				else // If file
				{
					fileSize = Utility.Byte8ToInt(fileSizeB);
					Reader.Position = chunkStartIndex += fileChunkLength; // Allign
				}

				// Finalise chunk and append it to parents children
				currentChunk.Flag = flags;
				currentChunk.Name = fileName;
				currentChunk.Metadata = metadata;
				currentChunk.Offset = fileOffset;
				currentChunk.Size = fileSize;

				Chunk.AddChild(parent, currentChunk);
			}
			return parent;
		}

		// Method to start processing LSPA
		public static Chunk.Node ReadLSPA(FileStream Reader, string filepath)
		{
			// Create Root node and Roots children
			Chunk.Node Root = new Chunk.Node();
			Root.Children = new List<Chunk.Node>();

			byte[] magic = Utility.Read(Reader, 4);
			if (!VerifyLMLM(magic))
			{
				Console.WriteLine("Unrecognised signature");
				Console.ReadLine();
				System.Environment.Exit(0);
			}

			// Get number of files in Root
			Reader.Position = 0x402;
			byte[] numOfFilesB = Utility.GetAllBytesBeforeNull(Utility.Read(Reader, 8));
			long numOfFiles = Utility.Byte8ToInt(numOfFilesB);

			Reader.Position = fileChunkLength;						// Allign
			Root = DirectoryGetEntries(Reader, Root, numOfFiles);   // Get entries in Root
			Root.Name = Path.GetFileNameWithoutExtension(filepath);
			return Root;
		}

		// Method to extract file from LSPA
		public static void ExtractFile(FileStream Reader, Chunk.Node chunk, string filePath)
		{
			// Get file data
			byte[] file = new byte[chunk.Size];
			Reader.Position = chunk.Offset;
			Reader.Read(file, 0, (int)chunk.Size);

			// Write file data
			BinaryWriter outputWriter = new BinaryWriter(File.Open(filePath, FileMode.Create, FileAccess.Write));
			outputWriter.Write(file);
			outputWriter.Close();

			// Set file attributes
			if (chunk.Flag.Hidden)
			{
				File.SetAttributes(filePath, FileAttributes.Hidden);
			}
			if (chunk.Flag.Metadata)
			{
				DateTime modifiedDate = Utility.GetModifiedDate(chunk.Metadata);
				File.SetLastWriteTime(filePath, modifiedDate);
			}
			if (chunk.Flag.ReadOnly)
			{
				File.SetAttributes(filePath, FileAttributes.ReadOnly);
			}
			if (chunk.Flag.Archive)
			{
				File.SetAttributes(filePath, FileAttributes.Archive);
			}
			if (chunk.Flag.System)
			{
				File.SetAttributes(filePath, FileAttributes.System);
			}
		}

		// Method to extarct files from LSPA file data (Recursive)
		public static void ExtractLSPA(FileStream Reader, Chunk.Node Root, string outputDir)
		{
			// Create new directory if necessary 
			if (!Directory.Exists(outputDir))
			{
				Directory.CreateDirectory(outputDir);
			}

			foreach (Chunk.Node child in Root.Children)
			{
				string filePath = String.Concat(outputDir, "/", child.Name);

				if (child.Children != null) // If direcotry
				{
					ExtractLSPA(Reader, child, filePath);
				}
				else // Else file
				{
					ExtractFile(Reader, child, filePath);
				}
			}
		}

		// Method to output LSPA entries as tree
		public static void TreeLSPA(Chunk.Node Root, string indent = "")
		{
			for (int i=0; i<Root.Children.Count; i++)
			{
				Chunk.Node child = Root.Children[i];
				bool isLast = i == Root.Children.Count - 1;
				bool isDirectory = child.Children != null;

				Console.Write(indent);
				if (isLast)
				{
					Console.Write("\\-");
				} else
				{
					Console.Write("|-");
				}
				Console.WriteLine(child.Name);

				if (isDirectory)
				{
					if (!isLast)
					{
						TreeLSPA(child, indent + "| ");
					} else
					{
						TreeLSPA(child, indent + "  ");
					}
					
				}
			}
		}

		// Lengths of segments for allignment
			// All lengths are a multiple of 512
		const int dataChunkLength = 0x200; // Length of standard allignment (512)
		const int dirChunkLength = 0x400;  // Length of directory chunks (1024)
		const int fileChunkLength = 0x600; // Legnth of file chunks (1536)

		// Flags
			// Flags are bitwise, which is why they're powers of 2
		const short flag_hidden = 1;
		const short flag_metadata = 2;
		const short flag_readOnly = 4;
		const short flag_archive = 8;
		const short flag_system = 16;
	}
}