using App.Linebot.Conversations.Core;
using App.Linebot.Persons;
using ImageSharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ProjectOxford.Face.Contract;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Yamac.LineMessagingApi.Events;
using Messages = Yamac.LineMessagingApi.Messages;
using Templates = Yamac.LineMessagingApi.Messages.Templates;

namespace App.Linebot.Conversations
{
    /// <summary>
    /// 会話:画像 の実装。
    /// </summary>
    [ConversationRoute("/image")]
    public class ImageConversation : ConversationBase
    {
        private LinebotOptions _options;

        private ILineMessagingService _lineMessagingService;

        private IPersonService _personService;

        private readonly ILogger _logger;

        public ImageConversation(
            IConversationRouteService conversationRouteService,
            IOptions<LinebotOptions> options,
            ILineMessagingService lineMessagingService,
            IPersonService personService,
            ILoggerFactory loggerFactory) : base(conversationRouteService)
        {
            _options = options.Value;
            _lineMessagingService = lineMessagingService;
            _personService = personService;
            _logger = loggerFactory.CreateLogger<RootConversation>();
        }

        /// <summary>
        /// 会話:顔の検出
        /// </summary>
        [ConversationPath("detect", ConversationAction.Start)]
        public async Task DetectStart(string senderId, JObject data)
        {
            var replyToken = data["ReplyToken"].ToString();
            var messageId = data["MessageId"].ToString();
            var rememberMode = bool.Parse(data["RememberMode"].ToString());

            // 画像をファイルに保存
            var filename = $"{messageId}";
            using (var stream = await _lineMessagingService.GetMessageContentAsync(messageId))
            using (var memoryStream = new MemoryStream())
            using (var fileStream = new FileStream($"{_options.WorkingFileStoreRoot}/{filename}", FileMode.CreateNew))
            {
                await stream.CopyToAsync(memoryStream);
                await fileStream.WriteAsync(memoryStream.ToArray(), 0, (int)memoryStream.Length);
            }

            // 画像の顔を検出
            using (var fileStream = new FileStream($"{_options.WorkingFileStoreRoot}/{filename}", FileMode.Open))
            {
                var faces = await _personService.DetectFaceAsync(fileStream);
                if (faces.Length == 1)
                {
                    // 顔が単数なら
                    await HandleSingleFaceAsync(senderId, replyToken, filename, faces[0], rememberMode);
                }
                else if (faces.Length >= 2)
                {
                    // 顔が複数なら
                    await HandleMultipleFacesAsync(senderId, replyToken, filename, faces, rememberMode);
                }
                else
                {
                    // 顔が検出されなければ
                    await _lineMessagingService.ReplyTextMessageAsync(replyToken, @"顔が写ってないみたい...");
                    await StartConversationAsync(senderId, "/");
                }
            }
        }

        /// <summary>
        /// 会話:顔の検出
        /// </summary>
        [ConversationPath("detect", ConversationAction.Reply)]
        public async Task DetectReply(Event theEvent, JObject data)
        {
            await StartConversationAsync(theEvent.Source.SenderId, "/");
        }

        /// <summary>
        /// 単数の顔の処理。
        /// </summary>
        private async Task HandleSingleFaceAsync(string senderId, string replyToken, string filename, Face face, bool rememberMode = false)
        {
            // PersonGroupを確保
            await _personService.EnsurePersonGroupAsync(senderId);

            // 検出された Face で Person を検索(RememberMode なら検索しない)
            Persons.Person person = null;
            if (!rememberMode)
            {
                person = await _personService.FindPersonByFaceAsync(senderId, face);
            }

            if (person == null)
            {
                // Person が見つからないか RememberMode なら 会話:この人(たち)は誰？ に移動
                var data = new JObject
                {
                    ["Filename"] = filename,
                    ["UnknownFaceIds"] = new JArray(new string[] { face.FaceId.ToString() }),
                    ["UnknownFaceRectangles"] = new JArray(new string[] {
                        $"{face.FaceRectangle.Left},{face.FaceRectangle.Top},{face.FaceRectangle.Width},{face.FaceRectangle.Height}"
                    }),
                };
                await StartConversationAsync(senderId, "/image/whoare/select", data);
            }
            else
            {
                // Person が見つかれば名前を発言して 会話:ルート に移動
                await _lineMessagingService.ReplyTextMessageAsync(replyToken, $@"この人は {person.Name} だね！");
                await StartConversationAsync(senderId, "/");
            }
        }

        /// <summary>
        /// 複数の顔の処理。
        /// </summary>
        private async Task HandleMultipleFacesAsync(string senderId, string replyToken, string filename, Face[] faces, bool rememberMode = false)
        {
            // PersonGroup を準備
            await _personService.EnsurePersonGroupAsync(senderId);

            Persons.Person[] knowns; // 分かった Person
            Face[] unknowns; // 分からなかった Face

            if (rememberMode)
            {
                // 全て分からないことにする
                knowns = new Persons.Person[0];
                unknowns = faces;
            }
            else
            {
                // 検出された Face で Person を検索
                var persons = await _personService.FindPersonByFacesAsync(senderId, faces);

                // 分かった Person と分からなかった Face を分類
                knowns = persons.Where(person => person != null).ToArray();
                unknowns = faces.Zip(persons, (face, person) =>
                {
                    return new { Face = face, Person = person };
                })
                .Where(pair => pair.Person == null)
                .Select(pair => pair.Face)
                .ToArray();
            }

            if (knowns.Length > 0)
            {
                // 分かった Person が一人以上いれば名前を発言
                var text = string.Join("、", knowns.Select(person => person.Name)) + @"が写ってるね！";
                if (faces.Length > 10)
                {
                    // Face が 10 以上なら unknowns 処理をしない
                    text += " 人数が多すぎてよく分からないよ！";
                }
                else if (unknowns.Length > 0)
                {
                    // 分からなかった Face があれば人数を発言
                    text += $@" でも他の{(faces.Length - knowns.Length)}人は知らないよ！ 誰なのか教えて！";
                }
                await _lineMessagingService.ReplyTextMessageAsync(replyToken, text);
            }
            else
            {
                await _lineMessagingService.ReplyTextMessageAsync(replyToken, @"誰も知らないよ！ 誰なのか教えて！");
            }

            if (unknowns.Length > 0)
            {
                // 分からなかった Face があれば 会話:この人たちは誰？ に移動
                var data = new JObject
                {
                    ["Filename"] = filename,
                    ["UnknownFaceIds"] = new JArray(unknowns.Select(unknown => unknown.FaceId.ToString())),
                    ["UnknownFaceRectangles"] = new JArray(unknowns.Select(unknown =>
                        $"{unknown.FaceRectangle.Left},{unknown.FaceRectangle.Top},{unknown.FaceRectangle.Width},{unknown.FaceRectangle.Height}")),
                };
                await StartConversationAsync(senderId, "/image/whoare/select", data);
            }
            else
            {
                // 分からなかった Face がなければ 会話:ルート に移動
                await StartConversationAsync(senderId, "/");
            }
        }

        /// <summary>
        /// 会話:この人たちは誰？
        /// </summary>
        [ConversationPath("whoare/select", ConversationAction.Start)]
        public async Task WhoareSelectStart(string senderId, JObject data)
        {
            var filename = data["Filename"].ToString();
            var faceIds = data["UnknownFaceIds"];
            var faceRectangles = data["UnknownFaceRectangles"];

            var columns = new List<Templates.CarouselColumn>();
            faceIds
                .Zip(faceRectangles, (faceId, faceRectangle) => new { FaceId = faceId, FaceRect = faceRectangle })
                .Take(5) // Carousel は最大 5 まで
                .Select((face, index) => new { Face = face, Index = index }) // Face とインデックスを選択
                .ToList()
                .ForEach(face =>
                {
                    // 顔の画像ファイルを生成
                    var faceFilename = $"{filename}-{face.Face.FaceId}.jpg";
                    var facePreviewFilename = $"{filename}-{face.Face.FaceId}.jpg";
                    var faceRectValues = face.Face.FaceRect.ToString().Split(',');
                    var faceRectangle = new FaceRectangle
                    {
                        Left = int.Parse(faceRectValues[0].ToString()),
                        Top = int.Parse(faceRectValues[1].ToString()),
                        Width = int.Parse(faceRectValues[2].ToString()),
                        Height = int.Parse(faceRectValues[3].ToString()),
                    };
                    CreateFaceImageFile(filename, faceFilename, facePreviewFilename, faceRectangle);

                    // CarouselColumn を生成
                    columns.Add(new Templates.CarouselColumn
                    {
                        Title = @"この人は誰？",
                        Text = @"教えて！",
                        ThumbnailImageUrl = $"{_options.GeneratedFilePublicUrlRoot}/{_options.GeneratedFilePublicUrlPath}/{facePreviewFilename}",
                        Actions = new List<Templates.Action>()
                        {
                            new Templates.PostbackAction
                            {
                                Label = @"この人が誰なのか教える",
                                Data = $"{face.Index}",
                            },
                            new Templates.PostbackAction
                            {
                                Label = @"やめる",
                                Data = "Cancel",
                            },
                        },
                    });
                });

            // この人たちは誰？ を発言
            var templateMessage = new Messages.TemplateMessage
            {
                AltText = faceIds.Count() == 1 ? @"この人は誰？" : @"この人たちは誰？",
                Template = new Templates.CarouselTemplate
                {
                    Columns = columns,
                }
            };
            await _lineMessagingService.PushMessageAsync(senderId, templateMessage);
        }

        /// <summary>
        /// 作業用とプレビュー用の顔の画像を生成する。
        /// </summary>
        private void CreateFaceImageFile(string filename, string faceFilename, string facePreviewFilename, FaceRectangle faceRectangle)
        {
            using (FileStream inputStream = File.OpenRead($"{_options.WorkingFileStoreRoot}/{filename}"))
            using (FileStream outputStream = File.OpenWrite($"{_options.WorkingFileStoreRoot}/{faceFilename}"))
            using (Image image = new Image(inputStream))
            {
                var cropRect = new Rectangle(
                    faceRectangle.Left,
                    faceRectangle.Top,
                    faceRectangle.Width,
                    faceRectangle.Height);
                image.Crop(cropRect)
                     .Save(outputStream);
            }

            using (FileStream inputStream = File.OpenRead($"{_options.WorkingFileStoreRoot}/{filename}"))
            using (FileStream outputStream = File.OpenWrite($"{_options.GeneratedFileStoreRoot}/{facePreviewFilename}"))
            using (Image image = new Image(inputStream))
            {
                var cropRect = new Rectangle(
                    (int)(faceRectangle.Left - faceRectangle.Width * 0.5),
                    (int)(faceRectangle.Top - faceRectangle.Height * 0.6),
                    (int)(faceRectangle.Width + faceRectangle.Width * 1),
                    (int)(faceRectangle.Height + faceRectangle.Height * 1));
                image.Crop(cropRect)
                     .Save(outputStream);
            }
        }

        /// <summary>
        /// 会話:この人たちは誰？
        /// </summary>
        [ConversationPath("whoare/select", ConversationAction.Reply)]
        public async Task WhoareSelectReply(Event theEvent, JObject data)
        {
            var senderId = theEvent.Source.SenderId;

            if (theEvent.Type == EventType.Message)
            {
                // Message なら
                var messageEvent = theEvent as MessageEvent;

                if (messageEvent.Message.Type == MessageType.Text)
                {
                    var text = ((theEvent as MessageEvent).Message as TextMessage).Text;
                    if (text.StartsWith(@"やめる"))
                    {
                        await _lineMessagingService.ReplyTextMessageAsync(messageEvent.ReplyToken, @"覚えるのやめるね。");
                        await StartConversationAsync(senderId, "/");
                        return;
                    }
                }

                await _lineMessagingService.ReplyTextMessageAsync(messageEvent.ReplyToken, $@"何？ 分からないから覚えるのやめるね。");
                await StartConversationAsync(senderId, "/");
                return;
            }
            else if (theEvent.Type != EventType.Postback)
            {
                // Postback 以外なら 会話のルート へ移動
                await StartConversationAsync(senderId, "/");
                return;
            }

            var postbackEvent = theEvent as PostbackEvent;
            var replyToken = postbackEvent.ReplyToken;

            // 教えるのをやめる選択なら 会話:ルート へ移動
            var postback = postbackEvent.Postback;
            if (postback.Data == "Cancel")
            {
                await _lineMessagingService.ReplyTextMessageAsync(replyToken, @"覚えるのやめるね。");
                await StartConversationAsync(senderId, "/");
                return;
            }

            // 会話:この人たちは誰の回答 へ移動
            data["ReplyToken"] = replyToken;
            int faceIndex;
            if (!int.TryParse(postback.Data, out faceIndex))
            {
                return;
            }
            data["FaceIndex"] = faceIndex;
            await StartConversationAsync(senderId, "/image/whoare/answer", data);
        }

        /// <summary>
        /// 会話:この人たちは誰の回答
        /// </summary>
        [ConversationPath("whoare/answer", ConversationAction.Start)]
        public async Task WhoareAnswerStart(string senderId, JObject data)
        {
            var replyToken = data["ReplyToken"].ToString();
            await _lineMessagingService.ReplyTextMessageAsync(replyToken, @"その人は誰？ 名前を教えて！");
        }

        /// <summary>
        /// 会話:この人たちは誰の回答
        /// </summary>
        [ConversationPath("whoare/answer", ConversationAction.Reply)]
        public async Task WhoareAnswerReply(Event theEvent, JObject data)
        {
            var senderId = theEvent.Source.SenderId;
            var faceIds = data["UnknownFaceIds"];
            var faceRectangles = data["UnknownFaceRectangles"];
            int faceIndex = int.Parse(data["FaceIndex"].ToString());
            if (faceIndex < 0 || faceIndex >= faceIds.Count())
            {
                await StartConversationAsync(senderId, "/");
                return;
            }
            var filename = data["Filename"].ToString();
            var faceId = faceIds[faceIndex].ToString();
            var faceFilename = $"{filename}-{faceId}.jpg";

            // Message 以外なら 会話:ルート へ移動
            if (theEvent.Type != EventType.Message)
            {
                await StartConversationAsync(senderId, "/");
                return;
            }

            var messageEvent = theEvent as MessageEvent;
            var replyToken = messageEvent.ReplyToken;

            // TextMessage 以外なら 会話:ルート へ移動
            if (messageEvent.Message.Type != MessageType.Text)
            {
                await _lineMessagingService.ReplyTextMessageAsync(replyToken, $@"何？ 分からないから覚えるのやめるね。");
                await StartConversationAsync(senderId, "/");
                return;
            }

            // 覚える
            var name = (messageEvent.Message as TextMessage).Text;
            await RememberPersonFaceAsync(senderId, name, faceFilename);

            // 単数なら 会話:ルート へ移動、複数なら継続
            if (faceIds.Count() == 1)
            {
                await _lineMessagingService.ReplyTextMessageAsync(replyToken, $@"この人は {name} だね？ 覚えたよ！");
                await StartConversationAsync(senderId, "/");
            }
            else
            {
                await _lineMessagingService.ReplyTextMessageAsync(replyToken, $@"この人は {name} だね？ 覚えたよ！ 他の人は？ もういいなら「やめる」って言ってね！");
                faceIds.ElementAt(faceIndex).Remove();
                faceRectangles.ElementAt(faceIndex).Remove();
                await StartConversationAsync(senderId, "/image/whoare/select", data);
            }
        }

        /// <summary>
        /// 顔を覚える。
        /// </summary>
        private async Task RememberPersonFaceAsync(string senderId, string name, string filename)
        {
            var person = await _personService.FindPersonByNameAsync(senderId, name);
            if (person == null)
            {
                person = await _personService.CreatePersonAsync(senderId, name);
            }

            using (var fileStream = new FileStream($"{_options.WorkingFileStoreRoot}/{filename}", FileMode.Open))
            {
                await _personService.RememberPersonFaceAsync(person, fileStream);
            }
        }
    }
}
