using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SaveAudioClip
{
	const int HEADER_SIZE = 44;

	public static bool Save(string filename, AudioClip clip)
	{
		if (!filename.ToLower().EndsWith(".wav"))
			filename += ".wav";
		var filepath = Path.Combine(Application.persistentDataPath, filename);
		_ = Directory.CreateDirectory(Path.GetDirectoryName(filepath));
		using var fileStream = CreateEmpty(filepath);
		ConvertAndWrite(fileStream, clip);
		WriteHeader(fileStream, clip);
		return true;
	}

	public static AudioClip TrimSilence(AudioClip clip, float min)
	{
		var samples = new float[clip.samples];
		_ = clip.GetData(samples, 0);
		return TrimSilence(new List<float>(samples), min, clip.channels, clip.frequency);
	}

	public static AudioClip TrimSilence(List<float> samples, float min, int channels, int hz)
	{
		return TrimSilence(samples, min, channels, hz, false);
	}

	public static AudioClip TrimSilence(List<float> samples, float min, int channels, int hz, bool stream)
	{
		int i;

		for (i = 0; i < samples.Count; i++)
			if (Mathf.Abs(samples[i]) > min)
				break;
		samples.RemoveRange(0, i);

		for (i = samples.Count - 1; i > 0; i--)
			if (Mathf.Abs(samples[i]) > min)
				break;
		samples.RemoveRange(i, samples.Count - i);

		var clip = AudioClip.Create("TempClip", samples.Count, channels, hz, stream);
		_ = clip.SetData(samples.ToArray(), 0);
		return clip;
	}

	static FileStream CreateEmpty(string filepath)
	{
		var fileStream = new FileStream(filepath, FileMode.Create);
		var emptyByte = new byte();
		for (var i = 0; i < HEADER_SIZE; i++)
			fileStream.WriteByte(emptyByte);
		return fileStream;
	}

	static void ConvertAndWrite(FileStream fileStream, AudioClip clip)
	{
		var samples = new float[clip.samples];
		_ = clip.GetData(samples, 0);
		var intData = new Int16[samples.Length];
		var bytesData = new Byte[samples.Length * 2];
		var rescaleFactor = 32767; //to convert float to Int16
		for (var i = 0; i < samples.Length; i++)
		{
			intData[i] = (short)(samples[i] * rescaleFactor);
			var byteArr = BitConverter.GetBytes(intData[i]);
			byteArr.CopyTo(bytesData, i * 2);
		}
		fileStream.Write(bytesData, 0, bytesData.Length);
	}

	static void WriteHeader(FileStream fileStream, AudioClip clip)
	{
		var hz = clip.frequency;
		var channels = clip.channels;
		var samples = clip.samples;
		_ = fileStream.Seek(0, SeekOrigin.Begin);

		var riff = System.Text.Encoding.UTF8.GetBytes("RIFF");
		fileStream.Write(riff, 0, 4);

		var chunkSize = BitConverter.GetBytes(fileStream.Length - 8);
		fileStream.Write(chunkSize, 0, 4);

		var wave = System.Text.Encoding.UTF8.GetBytes("WAVE");
		fileStream.Write(wave, 0, 4);

		var fmt = System.Text.Encoding.UTF8.GetBytes("fmt ");
		fileStream.Write(fmt, 0, 4);

		var subChunk1 = BitConverter.GetBytes(16);
		fileStream.Write(subChunk1, 0, 4);

		var audioFormat = BitConverter.GetBytes((ushort)1);
		fileStream.Write(audioFormat, 0, 2);

		var numChannels = BitConverter.GetBytes(channels);
		fileStream.Write(numChannels, 0, 2);

		var sampleRate = BitConverter.GetBytes(hz);
		fileStream.Write(sampleRate, 0, 4);

		var byteRate = BitConverter.GetBytes(hz * channels * 2);
		fileStream.Write(byteRate, 0, 4);

		var blockAlign = (ushort)(channels * 2);
		fileStream.Write(BitConverter.GetBytes(blockAlign), 0, 2);

		var bps = 16;
		var bitsPerSample = BitConverter.GetBytes(bps);
		fileStream.Write(bitsPerSample, 0, 2);

		var datastring = System.Text.Encoding.UTF8.GetBytes("data");
		fileStream.Write(datastring, 0, 4);

		var subChunk2 = BitConverter.GetBytes(samples * channels * 2);
		fileStream.Write(subChunk2, 0, 4);
	}
}