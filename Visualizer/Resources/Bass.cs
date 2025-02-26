namespace Visualizer
{
    public class BassTools
    {

        public float[,] GetChannelMatrix(SongData song, int inputChans, string stems, int outputChans = 2, bool isOgg = true)
        {
            //initialize matrix
            //matrix must be float[output_channels, input_channels]
            var matrix = new float[outputChans, inputChans];
            var ArrangedChannels = ArrangeStreamChannels(inputChans, isOgg);
            if (song.ChannelsDrums > 0 && (stems.Contains("drums") || stems.Contains("allstems")))
            {
                //for drums it's a bit tricky because of the possible combinations
                switch (song.ChannelsDrums)
                {
                    case 2:
                        //stereo kit
                        matrix = DoMatrixPanning(song, matrix, ArrangedChannels, 2, 0);
                        break;
                    case 3:
                        //mono kick
                        matrix = DoMatrixPanning(song, matrix, ArrangedChannels, 1, 0);
                        //stereo kit
                        matrix = DoMatrixPanning(song, matrix, ArrangedChannels, 2, 1);
                        break;
                    case 4:
                        //mono kick
                        matrix = DoMatrixPanning(song, matrix, ArrangedChannels, 1, 0);
                        //mono snare
                        matrix = DoMatrixPanning(song, matrix, ArrangedChannels, 1, 1);
                        //stereo kit
                        matrix = DoMatrixPanning(song, matrix, ArrangedChannels, 2, 2);
                        break;
                    case 5:
                        //mono kick
                        matrix = DoMatrixPanning(song, matrix, ArrangedChannels, 1, 0);
                        //stereo snare
                        matrix = DoMatrixPanning(song, matrix, ArrangedChannels, 2, 1);
                        //stereo kit
                        matrix = DoMatrixPanning(song, matrix, ArrangedChannels, 2, 3);
                        break;
                    case 6:
                        //stereo kick
                        matrix = DoMatrixPanning(song, matrix, ArrangedChannels, 2, 0);
                        //stereo snare
                        matrix = DoMatrixPanning(song, matrix, ArrangedChannels, 2, 2);
                        //stereo kit
                        matrix = DoMatrixPanning(song, matrix, ArrangedChannels, 2, 4);
                        break;
                }
            }
            //var channel = song.ChannelsDrums;
            if (song.ChannelsBass > 0 && (stems.Contains("bass") || stems.Contains("allstems")))
            {
                matrix = DoMatrixPanning(song, matrix, ArrangedChannels, song.ChannelsBass, song.ChannelsBassStart);//channel);
            }
            //channel = channel + song.ChannelsBass;
            if (song.ChannelsGuitar > 0 && (stems.Contains("guitar") || stems.Contains("allstems")))
            {
                matrix = DoMatrixPanning(song, matrix, ArrangedChannels, song.ChannelsGuitar, song.ChannelsGuitarStart);//channel);
            }
            //channel = channel + song.ChannelsGuitar;
            if (song.ChannelsVocals > 0 && (stems.Contains("vocals") || stems.Contains("allstems")))
            {
                matrix = DoMatrixPanning(song, matrix, ArrangedChannels, song.ChannelsVocals, song.ChannelsVocalsStart);//channel);
            }
            //channel = channel + song.ChannelsVocals;
            if (song.ChannelsKeys > 0 && (stems.Contains("keys") || stems.Contains("allstems")))
            {
                matrix = DoMatrixPanning(song, matrix, ArrangedChannels, song.ChannelsKeys, song.ChannelsKeysStart);//channel);
            }
            //channel = channel + song.ChannelsKeys;
            if (song.ChannelsCrowd > 0 && !stems.Contains("NOcrowd") && (stems.Contains("crowd") || stems.Contains("allstems")))
            {
                matrix = DoMatrixPanning(song, matrix, ArrangedChannels, song.ChannelsCrowd, song.ChannelsCrowdStart);//channel);
            }
            //channel = channel + song.ChannelsCrowd;
            if ((stems.Contains("backing") || stems.Contains("allstems"))) //song.ChannelsBacking > 0 &&  ---- should always be enabled per specifications
            {
                var backing = song.ChannelsTotal - song.ChannelsBass - song.ChannelsDrums - song.ChannelsGuitar - song.ChannelsKeys - song.ChannelsVocals - song.ChannelsCrowd;
                if (backing > 0) //backing not required 
                {
                    if (song.ChannelsCrowdStart + song.ChannelsCrowd == song.ChannelsTotal) //crowd channels are last
                    {
                        matrix = DoMatrixPanning(song, matrix, ArrangedChannels, backing, song.ChannelsCrowdStart - backing);//channel);                        
                    }
                    else
                    {
                        matrix = DoMatrixPanning(song, matrix, ArrangedChannels, backing, song.ChannelsTotal - backing);//channel);
                    }
                }
            }
            return matrix;
        }

        private float[,] DoMatrixPanning(SongData song, float[,] in_matrix, IList<int> ArrangedChannels, int inst_channels, int curr_channel)
        {
            //by default matrix values will be 0 = 0 volume
            //if nothing is assigned here, it stays at 0 so that channel won't be played
            //otherwise we assign a volume level based on the dta volumes

            //initialize output matrix based on input matrix, just in case something fails there's something going out
            var matrix = in_matrix;

            //split attenuation and panning info from DTA file for index access
            string[] volumes = new string[song.ChannelsTotal];
            try
            {
                volumes = song.OriginalAttenuationValues.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
            }
            catch { }
            string[] pans = new string[song.ChannelsTotal];
            try
            {
                pans = song.PanningValues.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
            }
            catch { }

            //BASS.NET lets us specify maximum volume when converting dB to Level
            //in case we want to change this later, it's only one value to change
            const double max_dB = 1.0;

            // Setting volume per track
            float vol;
            try
            {
                vol = ConvertDBToLevel(Convert.ToDouble(volumes[curr_channel]), max_dB);
            }
            catch (Exception)
            {
                vol = 1.0f;
            }

            //assign volume level to channels in the matrix
            if (inst_channels == 2) //is it a stereo track
            {
                try
                {
                    //assign current channel (left) to left channel
                    matrix[0, ArrangedChannels[curr_channel]] = vol;
                }
                catch (Exception)
                { }
                try
                {
                    //assign next channel (right) to the right channel
                    matrix[1, ArrangedChannels[curr_channel + 1]] = vol;
                }
                catch (Exception)
                { }
            }
            else
            {
                //it's a mono track, let's assign based on the panning value
                double pan;
                try
                {
                    pan = Convert.ToDouble(pans[curr_channel]);
                }
                catch (Exception)
                {
                    pan = 0.0; // in case there's an error above, it gets centered
                }

                if (pan <= 0) //centered or left, assign it to the left channel
                {
                    matrix[0, ArrangedChannels[curr_channel]] = vol;
                }
                if (pan >= 0) //centered or right, assign it to the right channel
                {
                    matrix[1, ArrangedChannels[curr_channel]] = vol;
                }
            }
            return matrix;
        }

        const double max_dB = 1.0;
        // Convert dB to linear volume level (alternative to Utils.DBToLevel)
        float ConvertDBToLevel(double dB, double maxDB = 1.0)
        {
            return (float)Math.Pow(10, dB / 20) * (float)maxDB;
        }

        public int[] ArrangeStreamChannels(int totalChannels, bool isOgg)
        {
            var channels = new int[totalChannels];
            if (isOgg)
            {
                switch (totalChannels)
                {
                    case 3:
                        channels[0] = 0;
                        channels[1] = 2;
                        channels[2] = 1;
                        break;
                    case 5:
                        channels[0] = 0;
                        channels[1] = 2;
                        channels[2] = 1;
                        channels[3] = 3;
                        channels[4] = 4;
                        break;
                    case 6:
                        channels[0] = 0;
                        channels[1] = 2;
                        channels[2] = 1;
                        channels[3] = 4;
                        channels[4] = 5;
                        channels[5] = 3;
                        break;
                    case 7:
                        channels[0] = 0;
                        channels[1] = 2;
                        channels[2] = 1;
                        channels[3] = 5;
                        channels[4] = 6;
                        channels[5] = 4;
                        channels[6] = 3;
                        break;
                    case 8:
                        channels[0] = 0;
                        channels[1] = 2;
                        channels[2] = 1;
                        channels[3] = 6;
                        channels[4] = 4;
                        channels[5] = 7;
                        channels[6] = 5;
                        channels[7] = 3;
                        break;
                    default:
                        goto DoAllChannels;
                }
                return channels;
            }
        DoAllChannels:
            for (var i = 0; i < totalChannels; i++)
            {
                channels[i] = i;
            }
            return channels;
        }

    }
}
