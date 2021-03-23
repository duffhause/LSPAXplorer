using System;
using System.Windows.Forms;
using System.IO;
using LucasDuff;
using Be.Windows.Forms;
using System.Drawing;
 
namespace LSPAXplorer
{
	public partial class Form1 : Form
	{
		LSPA.Chunk.Node Root;
		FileStream Reader;
		Liscences liscences;
		AxWMPLib.AxWindowsMediaPlayer player;
		string[] TextExtensions = { ".con", ".mfk", ".txt", ".xml", ".ini", ".spt", ".lua" };
		string[] SoundExtensions = { ".rsd", ".ogg", ".FLAC"};
		string[] VideoExtensions = { ".bik", ".rmv" };

		public Form1()
		{
			InitializeComponent();
			player = new AxWMPLib.AxWindowsMediaPlayer();
			liscences = new Liscences();
			liscences.Hide();
		}

		private void Form1_Load(object sender, EventArgs e)
		{

		}

		private void form_resize(object sender, EventArgs e)
		{
			
		}

		private void UpdateTree(TreeNode parentNode, LSPA.Chunk.Node currentChunk, string path)
		{
			if (path == Root.Name)
			{
				parentNode.Name = openFileDialog1.FileName;
				parentNode.Text = currentChunk.Name;
				parentNode.Tag = currentChunk;
			}
			int i = 0;
			foreach (LSPA.Chunk.Node child in currentChunk.Children)
			{
				int image = 0;
				bool isFolder = child.Children != null;
				if (isFolder) image = 1;
				parentNode.Nodes.Add(child.Name, child.Name, image);
				string filepath = String.Format("{0}/{1}", path, child.Name);
				parentNode.Nodes[i].Name = filepath;
				parentNode.Nodes[i].Tag = child;
				if (isFolder)
				{
					UpdateTree(parentNode.Nodes[i], child, filepath);
				}
				i++;
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (openFileDialog1.ShowDialog() != DialogResult.Cancel) 
			{
				Reader = File.OpenRead(openFileDialog1.FileName);
				Root = LSPA.ReadLSPA(Reader, openFileDialog1.FileName);

				if (treeView1.Nodes.Count != 0)
				{
					treeView1.Nodes.RemoveAt(0);
				}
				
				TreeNode rootTreeNode = new TreeNode();
				treeView1.Nodes.Add(rootTreeNode);
				UpdateTree(rootTreeNode, Root, Root.Name);
			}
		}

		private bool StringInArray(string str, string[] strArr)
		{
			foreach(string s in strArr)
			{
				if (s.ToLower() == str.ToLower()) return true;
			}
			return false;
		}

		private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
		{
			LSPA.Chunk.Node chunk = (LSPA.Chunk.Node)e.Node.Tag;
			hexBox1.ByteProvider = new DynamicByteProvider(LSPA.GetFile(Reader, chunk));
			e.Node.SelectedImageIndex = e.Node.ImageIndex;
			
			// Remove current preview
			if (this.splitContainer2.Panel1.Controls.Count > 0)
			{
				this.splitContainer2.Panel1.Controls.Remove(this.splitContainer2.Panel1.Controls[0]);
			}
			try
			{
				player.Ctlcontrols.stop();
				player.Dispose();
				player = new AxWMPLib.AxWindowsMediaPlayer();
			} catch (System.Windows.Forms.AxHost.InvalidActiveXStateException)
			{
				
			} 

			// Add new preview
			string ext = Path.GetExtension(chunk.Name);

			if (ext == ".png")
			{
				System.Windows.Forms.PictureBox pictureBox1 = new System.Windows.Forms.PictureBox();
				pictureBox1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
				pictureBox1.Dock = System.Windows.Forms.DockStyle.Fill;
				pictureBox1.Location = new System.Drawing.Point(0, 0);
				using (var ms = new MemoryStream(LSPA.GetFile(Reader, chunk)))
				{
					pictureBox1.Image = Image.FromStream(ms);
				}
				this.splitContainer2.Panel1.Controls.Add(pictureBox1);
			} else if (StringInArray(ext, TextExtensions)) {
				System.Windows.Forms.TextBox textBox = new System.Windows.Forms.TextBox();
				textBox.Multiline = true;
				textBox.Dock = System.Windows.Forms.DockStyle.Fill;
				textBox.Text = System.Text.Encoding.Default.GetString(LSPA.GetFile(Reader, chunk));
				textBox.ScrollBars = ScrollBars.Both;
				this.splitContainer2.Panel1.Controls.Add(textBox);
			} else if (StringInArray(ext, SoundExtensions))
			{
				PlayInPlayer(chunk, true, ".mp3");
			} else if (StringInArray(ext, VideoExtensions))
			{
				PlayInPlayer(chunk, true, ".mp4");
			}
		}

		private void PlayInPlayer(LSPA.Chunk.Node chunk, bool convert, string codec = "")
		{
			player = new AxWMPLib.AxWindowsMediaPlayer();
			Console.WriteLine(player.GetType());
			this.Controls.Add(player);
			this.splitContainer2.Panel1.Controls.Add(player);
			player.Dock = DockStyle.Fill;
			string ffmpeg = String.Format("{0}{1}", Path.GetTempPath(), "lspaxffmpeg.exe");
			string tmp = String.Format("{0}{1}", Path.GetTempPath(), chunk.Name);
			string tmp0 = String.Concat(tmp, codec);

			BinaryWriter outputWriter = new BinaryWriter(File.Open(tmp, FileMode.Create, FileAccess.Write));
			outputWriter.Write(LSPA.GetFile(Reader, chunk));
			outputWriter.Close();

			if (convert)
			{
				if (!File.Exists(ffmpeg))
				{
					outputWriter = new BinaryWriter(File.Open(ffmpeg, FileMode.Create, FileAccess.Write));
					outputWriter.Write(LSPAXplorer.Properties.Resources.ffmpeg);
					outputWriter.Close();
				}

				string command = String.Format("-i \"{0}\" -y \"{1}\"", tmp, tmp0);
				System.Diagnostics.Process process = new System.Diagnostics.Process();
				System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
				startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
				startInfo.FileName = ffmpeg;
				startInfo.Arguments = command;
				process.StartInfo = startInfo;
				process.Start();
				process.WaitForExit();
			}

			player.CreateControl();
			player.URL = tmp0;
			player.Ctlcontrols.stop();
		}

		private void extractToolStripMenuItem_Click(object sender, EventArgs e)
		{
			LSPA.Chunk.Node chunk = (LSPA.Chunk.Node)treeView1.SelectedNode.Tag;
			saveFileDialog1.FileName = chunk.Name;
			if (saveFileDialog1.ShowDialog() != DialogResult.Cancel)
			{
				if (chunk.Children == null)
				{
					LSPA.ExtractFile(Reader, chunk, string.Format("{1}{0}", chunk.Name, saveFileDialog1.FileName));
				}
				else
				{
					LSPA.ExtractLSPA(Reader, chunk, saveFileDialog1.FileName);
				}
			}
		}

		private void treeview1_MouseDown(object sender, TreeNodeMouseClickEventArgs e)
		{
			if(e.Button == MouseButtons.Right) treeView1.SelectedNode = e.Node;
		}

		private void splitContainer1_Panel2_Paint(object sender, PaintEventArgs e)
		{

		}

		private void hexBox1_Click_1(object sender, EventArgs e)
		{

		}

		private void button2_Click(object sender, EventArgs e)
		{
			liscences.ShowDialog();
		}
	}
}
