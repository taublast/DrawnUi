using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace PreviewTests;

public class MockChatViewModel : BindableObject
{

    public class ChatMessageDto
    {
        public string Id { get; set; }
        /// <summary>
        /// Profile Key
        /// </summary>
        public string Author { get; set; }

        public string Text { get; set; }

        /// <summary>
        /// Utc
        /// </summary>
        public DateTime CreatedTime { get; set; }
    }



    private bool _IsBusy;
    public bool IsBusy
    {
        get
        {
            return _IsBusy;
        }
        set
        {
            if (_IsBusy != value)
            {
                _IsBusy = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<ChatMessage> Items { get; } = new();

    public async Task LoadData()
    {

        try
        {
            IsBusy = true;

            using var stream = await FileSystem.OpenAppPackageFileAsync("Json/chat.json");
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            var messages = JsonConvert.DeserializeObject<List<ChatMessage>>(json);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var chatMessage in messages)
                {
                    Items.Add(chatMessage);
                }
            });

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        finally
        {
            IsBusy = false;
        }


    }
}
