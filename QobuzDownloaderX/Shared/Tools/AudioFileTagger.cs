using QobuzDownloaderX.Models;
using System;
using System.IO;
using TagLib;

namespace QobuzDownloaderX.Shared
{
    public static class AudioFileTagger
    {
        // Add Metadata to audiofiles in ID3v2 for mp3 and Vorbis Comment for FLAC
        public static void AddMetaDataTags(DownloadItemInfo fileInfo, string tagFilePath, string tagCoverArtFilePath, DownloadLogger logger)
        {
            // Set file to tag
            var tfile = TagLib.File.Create(tagFilePath);
            tfile.RemoveTags(TagTypes.Id3v1);

            // Use ID3v2.4 as default mp3 tag version
            TagLib.Id3v2.Tag.DefaultVersion = 4;
            TagLib.Id3v2.Tag.ForceDefaultVersion = true;

            switch (Globals.AudioFileType)
            {
                case ".mp3":

                    // For custom / troublesome tags.
                    TagLib.Id3v2.Tag customId3v2 = (TagLib.Id3v2.Tag)tfile.GetTag(TagTypes.Id3v2, true);

                    // Saving cover art to file(s)
                    if (Globals.TaggingOptions.WriteCoverImageTag)
                    {
                        int maxRetries = 2; // Define the maximum number of retries
                        int retryDelay = 500; // Define the delay in milliseconds between retries
                        int attempt = 0;

                    retry:
                        try
                        {
                            // Define cover art to use for MP3 file(s)
                            TagLib.Id3v2.AttachmentFrame pic = new TagLib.Id3v2.AttachmentFrame
                            {
                                TextEncoding = TagLib.StringType.Latin1,
                                MimeType = System.Net.Mime.MediaTypeNames.Image.Jpeg,
                                Type = TagLib.PictureType.FrontCover,
                                Data = TagLib.ByteVector.FromPath(tagCoverArtFilePath)
                            };

                            // Save cover art to MP3 file.
                            tfile.Tag.Pictures = new TagLib.IPicture[1] { pic };
                            tfile.Save();
                        }
                        catch (IOException)
                        {
                            attempt++;
                            if (attempt >= maxRetries)
                            {
                                logger.AddDownloadLogErrorLine($"Cover art tag failed, .jpg still exists?...{Environment.NewLine}", true, true);
                                break;
                            }
                            System.Threading.Thread.Sleep(retryDelay);
                            goto retry;
                        }
                        catch
                        {
                            logger.AddDownloadLogErrorLine($"Cover art tag failed, .jpg still exists?...{Environment.NewLine}", true, true);
                        }
                    }

                    // Track Title tag, version is already added to name if available
                    if (Globals.TaggingOptions.WriteTrackTitleTag) { tfile.Tag.Title = fileInfo.TrackName; }

                    // Album Title tag, version is already added to name if available
                    if (Globals.TaggingOptions.WriteAlbumNameTag) { tfile.Tag.Album = fileInfo.AlbumName; }

                    // Album Artist tag
                    if (Globals.TaggingOptions.WriteAlbumArtistTag)
                    {
                        if (Globals.TaggingOptions.MergePerformers)
                        {
                            tfile.Tag.AlbumArtists = new string[] { fileInfo.AlbumArtist };
                        }
                        else
                        {
                            tfile.Tag.AlbumArtists = fileInfo.AlbumArtists;
                        }
                    }

                    // Track Artist tag
                    if (Globals.TaggingOptions.WriteTrackArtistTag)
                    {
                        if (Globals.TaggingOptions.MergePerformers)
                        {
                            tfile.Tag.Performers = new string[] { fileInfo.PerformerName };
                        }
                        else
                        {
                            tfile.Tag.Performers = fileInfo.PerformerNames;
                        }
                    }

                    // Composer tag
                    if (Globals.TaggingOptions.WriteComposerTag)
                    {
                        if (Globals.TaggingOptions.MergePerformers)
                        {
                            tfile.Tag.Composers = new string[] { fileInfo.ComposerName };
                        }
                        else
                        {
                            tfile.Tag.Composers = fileInfo.ComposerNames;
                        }
                    }

                    // Label tag
                    if (Globals.TaggingOptions.WriteLabelTag) { tfile.Tag.Publisher = fileInfo.LabelName; }

                    // InvolvedPeople tag
                    if (Globals.TaggingOptions.WriteInvolvedPeopleTag) { customId3v2.SetTextFrame("TIPL", fileInfo.InvolvedPeople); }

                    // Release Year tag (writes to "TDRC" (recording date) Frame)
                    if (Globals.TaggingOptions.WriteReleaseYearTag) { tfile.Tag.Year = UInt32.Parse(fileInfo.ReleaseDate.Substring(0, 4)); }

                    // Release Date tag (use "TDRL" (release date) Frame for full date)
                    if (Globals.TaggingOptions.WriteReleaseDateTag) { customId3v2.SetTextFrame("TDRL", fileInfo.ReleaseDate); }

                    // Genre tag
                    if (Globals.TaggingOptions.WriteGenreTag) { tfile.Tag.Genres = new string[] { fileInfo.Genre }; }

                    // Disc Number tag
                    if (Globals.TaggingOptions.WriteDiskNumberTag) { tfile.Tag.Disc = Convert.ToUInt32(fileInfo.DiscNumber); }

                    // Total Discs tag
                    if (Globals.TaggingOptions.WriteDiskTotalTag) { tfile.Tag.DiscCount = Convert.ToUInt32(fileInfo.DiscTotal); }

                    // Total Tracks tag
                    if (Globals.TaggingOptions.WriteTrackTotalTag) { tfile.Tag.TrackCount = Convert.ToUInt32(fileInfo.TrackTotal); }

                    // Track Number tag
                    // !! Set Track Number after Total Tracks to prevent taglib-sharp from re-formatting the field to a "two-digit zero-filled value" !!
                    if (Globals.TaggingOptions.WriteTrackNumberTag)
                    {
                        // Set TRCK tag manually to prevent using "two-digit zero-filled value"
                        // See https://github.com/mono/taglib-sharp/pull/240 where this change was introduced in taglib-sharp v2.3
                        // Original command: tfile.Tag.Track = Convert.ToUInt32(TrackNumber);
                        customId3v2.SetNumberFrame("TRCK", Convert.ToUInt32(fileInfo.TrackNumber), tfile.Tag.TrackCount);
                    }

                    // Comment tag
                    if (Globals.TaggingOptions.WriteCommentTag) { tfile.Tag.Comment = Globals.TaggingOptions.CommentTag; }

                    // Copyright tag
                    if (Globals.TaggingOptions.WriteCopyrightTag) { tfile.Tag.Copyright = fileInfo.Copyright; }

                    // ISRC tag
                    if (Globals.TaggingOptions.WriteIsrcTag) { tfile.Tag.ISRC = fileInfo.Isrc; }

                    // Release Type tag
                    if (fileInfo.MediaType != null && Globals.TaggingOptions.WriteMediaTypeTag) { customId3v2.SetTextFrame("TMED", fileInfo.MediaType); }

                    // Album store URL tag
                    if (fileInfo.Url != null && Globals.TaggingOptions.WriteUrlTag) { customId3v2.SetTextFrame("WCOM", fileInfo.Url); }

                    // Save all selected tags to file
                    tfile.Save();

                    break;

                case ".flac":

                    // For custom / troublesome tags.
                    TagLib.Ogg.XiphComment custom = (TagLib.Ogg.XiphComment)tfile.GetTag(TagLib.TagTypes.Xiph);

                    // Saving cover art to file(s)
                    if (Globals.TaggingOptions.WriteCoverImageTag)
                    {
                        int maxRetries = 2; // Define the maximum number of retries
                        int retryDelay = 500; // Define the delay in milliseconds between retries
                        int attempt = 0;

                    retry:
                        try
                        {
                            // Define cover art to use for FLAC file(s)
                            TagLib.Id3v2.AttachmentFrame pic = new TagLib.Id3v2.AttachmentFrame
                            {
                                TextEncoding = TagLib.StringType.Latin1,
                                MimeType = System.Net.Mime.MediaTypeNames.Image.Jpeg,
                                Type = TagLib.PictureType.FrontCover,
                                Data = TagLib.ByteVector.FromPath(tagCoverArtFilePath)
                            };

                            // Save cover art to FLAC file.
                            tfile.Tag.Pictures = new TagLib.IPicture[1] { pic };
                            tfile.Save();
                        }
                        catch (IOException)
                        {
                            attempt++;
                            if (attempt >= maxRetries)
                            {
                                logger.AddDownloadLogErrorLine($"Cover art tag failed, .jpg still exists?...{Environment.NewLine}", true, true);
                                break;
                            }
                            System.Threading.Thread.Sleep(retryDelay);
                            goto retry;
                        }
                        catch
                        {
                            logger.AddDownloadLogErrorLine($"Cover art tag failed, .jpg still exists?...{Environment.NewLine}", true, true);
                        }
                    }

                    // Track Title tag, version is already added to name if available
                    if (Globals.TaggingOptions.WriteTrackTitleTag) { tfile.Tag.Title = fileInfo.TrackName; }

                    // Album Title tag, version is already added to name if available
                    if (Globals.TaggingOptions.WriteAlbumNameTag) { tfile.Tag.Album = fileInfo.AlbumName; }

                    // Album Artist tag
                    if (Globals.TaggingOptions.WriteAlbumArtistTag)
                    {
                        if (Globals.TaggingOptions.MergePerformers)
                        {
                            tfile.Tag.AlbumArtists = new string[] { fileInfo.AlbumArtist };
                        }
                        else
                        {
                            tfile.Tag.AlbumArtists = fileInfo.AlbumArtists;
                        }
                    }

                    // Track Artist tag
                    if (Globals.TaggingOptions.WriteTrackArtistTag)
                    {
                        if (Globals.TaggingOptions.MergePerformers)
                        {
                            tfile.Tag.Performers = new string[] { fileInfo.PerformerName };
                        }
                        else
                        {
                            tfile.Tag.Performers = fileInfo.PerformerNames;
                        }
                    }

                    // Composer tag
                    if (Globals.TaggingOptions.WriteComposerTag)
                    {
                        if (Globals.TaggingOptions.MergePerformers)
                        {
                            tfile.Tag.Composers = new string[] { fileInfo.ComposerName };
                        }
                        else
                        {
                            tfile.Tag.Composers = fileInfo.ComposerNames;
                        }
                    }

                    // Label tag
                    if (Globals.TaggingOptions.WriteLabelTag)
                    {
                        tfile.Tag.Publisher = fileInfo.LabelName; // Writes to the official ORGANIZATION field
                        custom.SetField("LABEL", fileInfo.LabelName);
                    }

                    // Producer tag
                    if (Globals.TaggingOptions.WriteProducerTag)
                    {
                        if (Globals.TaggingOptions.MergePerformers)
                        {
                            custom.SetField("PRODUCER", fileInfo.ProducerName);
                        }
                        else
                        {
                            custom.SetField("PRODUCER", fileInfo.ProducerNames);
                        }
                    }

                    // InvolvedPeople tag
                    if (Globals.TaggingOptions.WriteInvolvedPeopleTag) { custom.SetField("INVOLVEDPEOPLE", fileInfo.InvolvedPeople); }

                    // Release Year tag (The "tfile.Tag.Year" field actually writes to the DATE tag, so use custom tag)
                    if (Globals.TaggingOptions.WriteReleaseYearTag) { custom.SetField("YEAR", fileInfo.ReleaseDate?[..4] ?? "0000"); }

                    // Release Date tag
                    if (Globals.TaggingOptions.WriteReleaseDateTag) { custom.SetField("DATE", fileInfo.ReleaseDate); }

                    // Genre tag
                    if (Globals.TaggingOptions.WriteGenreTag) { tfile.Tag.Genres = new string[] { fileInfo.Genre }; }

                    // Track Number tag
                    if (Globals.TaggingOptions.WriteTrackNumberTag)
                    {
                        tfile.Tag.Track = Convert.ToUInt32(fileInfo.TrackNumber);
                        // Override TRACKNUMBER tag again to prevent using "two-digit zero-filled value"
                        // See https://github.com/mono/taglib-sharp/pull/240 where this change was introduced in taglib-sharp v2.3
                        custom.SetField("TRACKNUMBER", Convert.ToUInt32(fileInfo.TrackNumber));
                    }

                    // Disc Number tag
                    if (Globals.TaggingOptions.WriteDiskNumberTag) { tfile.Tag.Disc = Convert.ToUInt32(fileInfo.DiscNumber); }

                    // Total Discs tag
                    if (Globals.TaggingOptions.WriteDiskTotalTag) { tfile.Tag.DiscCount = Convert.ToUInt32(fileInfo.DiscTotal); }

                    // Total Tracks tag
                    if (Globals.TaggingOptions.WriteTrackTotalTag) { tfile.Tag.TrackCount = Convert.ToUInt32(fileInfo.TrackTotal); }

                    // Comment tag
                    if (Globals.TaggingOptions.WriteCommentTag) { tfile.Tag.Comment = Globals.TaggingOptions.CommentTag; }

                    // Copyright tag
                    if (Globals.TaggingOptions.WriteCopyrightTag) { tfile.Tag.Copyright = fileInfo.Copyright; }

                    // UPC tag
                    if (Globals.TaggingOptions.WriteUpcTag) { custom.SetField("UPC", fileInfo.Upc); }

                    // ISRC tag
                    if (Globals.TaggingOptions.WriteIsrcTag) { tfile.Tag.ISRC = fileInfo.Isrc; }

                    // Release Type tag
                    if (fileInfo.MediaType != null && Globals.TaggingOptions.WriteMediaTypeTag)
                    {
                        custom.SetField("MEDIATYPE", fileInfo.MediaType);
                    }

                    // Explicit tag
                    if (Globals.TaggingOptions.WriteExplicitTag)
                    {
                        if (fileInfo.Advisory == true) { custom.SetField("ITUNESADVISORY", "1"); } else { custom.SetField("ITUNESADVISORY", "0"); }
                    }

                    // Album store URL tag
                    if (fileInfo.Url != null && Globals.TaggingOptions.WriteUrlTag) { custom.SetField("URL", fileInfo.Url); }

                    // Save all selected tags to file
                    tfile.Save();

                    break;
            }
        }
    }
}
