using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AppoMobi.Forms.Controls;
using AppoMobi.Specials;
using Newtonsoft.Json;

namespace PreviewTests
{
    public class ChatMessageMetadata
    {
        public ChatMetaType Type { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Url { get; set; }
        public string Domain { get; set; }
        public string SiteName { get; set; }
        public string Image { get; set; }
        public string Video { get; set; }
    }

    public enum ChatMessageType
    {
        Default,
        Attachment,
        Call,
        Action //use attachment field for more
    }

    public class ChatMessage : MockChatViewModel.ChatMessageDto, INotifyPropertyChanged
    {
        public string Id { get; set; }

        public int DbId { get; set; }

        public string Author { get; set; }

        public string Meta { get; set; }

        public ChatMessageType MessageType { get; set; }

        public DateTime CreatedTime { get; set; }

        public string Group { get; set; }

        [JsonProperty("AType")]
        public int AttachmentType { get; set; }



        [JsonProperty("AStatus")]
        public int AttachmentStatus { get; set; }

        //todo attachments
        [JsonProperty("Thumb")]
        public string Thumbnail { get; set; }

        /// <summary>
        /// Blacklisted
        /// </summary>
        public bool Blocked { get; set; }

        /// <summary>
        /// by server or from client when sending
        /// </summary>
        public bool MetaChecked { get; set; }

        private bool _Notify;
        public bool Notify
        {
            get { return _Notify; }
            set
            {
                if (_Notify != value)
                {
                    _Notify = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool Outgoing
        {
            get
            {
                return Author == "71980d5c-2659-4ccb-ba79-e4433514fae5";
            }
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            var changed = PropertyChanged;
            if (changed == null)
                return;

            changed.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region DTO UPDATABLES

        private int _Status;
        public new int Status
        {
            get { return _Status; }
            set
            {
                if (_Status != value)
                {
                    _Status = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _Text;
        public new string Text
        {
            get { return _Text; }
            set
            {
                if (_Text != value)
                {
                    _Text = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _Attachment;
        public new string Attachment
        {
            get { return _Attachment; }
            set
            {
                if (_Attachment != value)
                {
                    _Attachment = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        private string _ImageMain;
        public string ImageMain
        {
            get { return _ImageMain; }
            set
            {
                if (_ImageMain != value)
                {
                    _ImageMain = value;
                    OnPropertyChanged();
                }
            }
        }

        public void Init()
        {

            try
            {
                if (!string.IsNullOrEmpty(this.Meta))
                    Metadata = JsonConvert.DeserializeObject<ChatMessageMetadata>(this.Meta);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            if (!string.IsNullOrEmpty(Thumbnail))
                ImageMain = Thumbnail;
            else
                ImageMain = Attachment;

            Sent = Status == 1;

            //RU
            DisplayDateTime = $"{this.CreatedTime.UtcToLocal():HH:mm d.m.yy}";
            DisplayTime = $"{this.CreatedTime.UtcToLocal():HH:mm}";

            var lastOnline = CreatedTime.UtcToLocal();
            var todayDate = DateTime.Now;
            var today = todayDate.Date;
            var when = lastOnline.ToLocalTime().Date;
            var delta = when - today;
            var days = Math.Abs(delta.Days);

            WhenDesc = "todo when";//ProjectExtensions.CorrectStringUponNumber(days,
            //    ResStrings.ExplainDate_Today,
            //    ResStrings.ExplainDate_Yest,
            //    ResStrings.ExplainDate_X1past,
            //    ResStrings.ExplainDate_X2past,
            //    ResStrings.ExplainDate_Xpast);

            OnPropertyChanged("Outgoing");
            OnPropertyChanged("IsText");

        }

        private bool _Read;
        public bool Read
        {
            get { return _Read; }
            set
            {
                if (_Read != value)
                {
                    _Read = value;
                    OnPropertyChanged();
                }
            }
        }



        private string _DisplayDateTime;
        public string DisplayDateTime
        {
            get { return _DisplayDateTime; }
            set
            {
                if (_DisplayDateTime != value)
                {
                    _DisplayDateTime = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _WhenDesc;
        public string WhenDesc
        {
            get { return _WhenDesc; }
            set
            {
                if (_WhenDesc != value)
                {
                    _WhenDesc = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _IsFirst;
        public bool IsFirst
        {
            get { return _IsFirst; }
            set
            {
                if (_IsFirst != value)
                {
                    _IsFirst = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _IsFirstDate;
        public bool IsFirstDate
        {
            get { return _IsFirstDate; }
            set
            {
                if (_IsFirstDate != value)
                {
                    _IsFirstDate = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _Delivered;
        public bool Delivered
        {
            get { return _Delivered; }
            set
            {
                if (_Delivered != value)
                {
                    _Delivered = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _Sent;
        public bool Sent
        {
            get { return _Sent; }
            set
            {
                if (_Sent != value)
                {
                    _Sent = value;
                    OnPropertyChanged();
                }
            }
        }


        private ChatMessageMetadata _metadata;
        public ChatMessageMetadata Metadata
        {
            get { return _metadata; }
            set
            {
                if (_metadata != value)
                {
                    _metadata = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _DisplayTime;
        public string DisplayTime
        {
            get { return _DisplayTime; }
            set
            {
                if (_DisplayTime != value)
                {
                    _DisplayTime = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _IsBusy;
        public bool IsBusy
        {
            get { return _IsBusy; }
            set
            {
                if (_IsBusy != value)
                {
                    _IsBusy = value;
                    OnPropertyChanged();
                }
            }
        }


        public ChatMetaType PresentAs
        {
            get
            {
                if (AttachmentType == (int)ChatMetaType.Separator)
                    return ChatMetaType.Separator;

                if (AttachmentType == (int)ChatMetaType.System)
                    return ChatMetaType.System;

                if (AttachmentType == (int)ChatMetaType.Image)
                    return ChatMetaType.Image;

                if (AttachmentType == (int)ChatMetaType.Video)
                    return ChatMetaType.Video;

                if (AttachmentType > 0)
                    return ChatMetaType.File;

                if (Metadata != null)
                    return Metadata.Type;

                return ChatMetaType.Default;
            }
        }

        public bool Attached { get; set; }

        protected string Linkify(string SearchText)
        {
            if (string.IsNullOrEmpty(SearchText))
                return "";
            // this will find links like:
            // http://www.mysite.com
            // as well as any links with other characters directly in front of it like:
            // href="http://www.mysite.com"
            // you can then use your own logic to determine which links to linkify
            Regex regx = new Regex(@"\b(((\S+)?)(@|mailto\:|(news|(ht|f)tp(s?))\://)\S+)\b", RegexOptions.IgnoreCase);
            SearchText = SearchText.Replace("&nbsp;", " ");
            MatchCollection matches = regx.Matches(SearchText);

            foreach (Match match in matches)
            {
                if (match.Value.StartsWith("http"))
                { // if it starts with anything else then dont linkify -- may already be linked!
                    SearchText = SearchText.Replace(match.Value, "<a href=\"" + match.Value + "\">" + match.Value + "</a>");
                }
            }

            return SearchText;
        }

        //        var invisibleGlyph = "⠀";




        private string _PlayerName = "Аноним";
        public string PlayerName
        {
            get { return _PlayerName; }
            set
            {
                if (_PlayerName != value)
                {
                    _PlayerName = value;
                    OnPropertyChanged();
                }
            }
        }

        private Player _player;
        public Player Player
        {
            get { return _player; }
            set
            {
                if (_player != value)
                {
                    _player = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
