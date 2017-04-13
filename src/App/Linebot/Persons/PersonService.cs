using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace App.Linebot.Persons
{
    /// <summary>
    /// <see cref="IPersonService"/> の実装。
    /// グループと人を管理する。顔を認識して人と結びつける。
    /// </summary>
    public class PersonService : IPersonService
    {
        private readonly MainDbContext _db;

        private readonly FaceServiceClient _faceServiceClient;

        private readonly ILogger _logger;

        public PersonService(MainDbContext db, FaceServiceClient faceServiceClient, ILoggerFactory loggerFactory)
        {
            _db = db;
            _faceServiceClient = faceServiceClient;
            _logger = loggerFactory.CreateLogger<PersonService>();
        }

        /// <summary>
        /// SenderId を PersonGroupId に変換する。
        /// </summary>
        /// <param name="senderId">LINE Messagin Service の SenderId</param>
        private string SenderIdToPersonGroupId(string senderId)
        {
            return senderId.ToLowerInvariant();
        }

        /// <summary>
        /// PersonGroup を準備する。
        /// </summary>
        /// <param name="senderId">LINE Messagin Service の SenderId</param>
        public async Task<PersonGroup> EnsurePersonGroupAsync(string senderId)
        {
            var personGroupId = SenderIdToPersonGroupId(senderId);

            // PersonGroup がデータベース上に見つかればそれを返す
            var personGroup = await _db.PersonGroup.Where(p => p.PersonGroupId == personGroupId).FirstOrDefaultAsync();
            if (personGroup != null)
            {
                return personGroup;
            }

            // Cognitive Face 上に PersonGroup を生成
            try
            {
                await _faceServiceClient.CreatePersonGroupAsync(personGroupId, personGroupId);
            }
            catch (FaceAPIException e)
            {
                // 既に存在している以外のエラーの場合は例外をリスロー
                if (e.ErrorCode != "PersonGroupExists")
                {
                    throw e;
                }
            }

            // データベース上に PersonGroup を生成
            personGroup = new PersonGroup
            {
                PersonGroupId = personGroupId,
                Name = personGroupId,
            };
            _db.PersonGroup.Add(personGroup);
            await _db.SaveChangesAsync();

            // PersonGroup を返す
            return personGroup;
        }

        /// <summary>
        /// 画像から顔を検出する。
        /// </summary>
        /// <param name="imageStream">画像のストリーム</param>
        /// <returns>検出された <see cref="Face"/> の配列を返す。</returns>
        public async Task<Face[]> DetectFaceAsync(Stream imageStream)
        {
            // Cognitive Face Detect API で顔を検出
            return await _faceServiceClient.DetectAsync(imageStream);
        }

        /// <summary>
        /// 検出した顔から人を認識する。
        /// </summary>
        /// <param name="senderId">LINE Messagin Service の SenderId</param>
        /// <param name="face">検出した顔</param>
        /// <returns>認識した <see cref="Person"/> を返す。認識できなかった場合は null を返す。</returns>
        public async Task<Person> FindPersonByFaceAsync(string senderId, Face face)
        {
            var persons = await FindPersonByFacesAsync(senderId, new Face[] { face });
            if (persons.Length == 0)
            {
                return null;
            }
            else
            {
                return persons[0];
            }
        }

        /// <summary>
        /// 検出した顔から人を認識する。
        /// </summary>
        /// <param name="senderId">LINE Messagin Service の SenderId</param>
        /// <param name="faces">検出した顔(複数)</param>
        /// <returns>認識した <see cref="Person"/> の配列を返す。認識できなかった場合は要素数 0 の配列を返す。</returns>
        public async Task<Person[]> FindPersonByFacesAsync(string senderId, Face[] faces)
        {
            var personGroupId = SenderIdToPersonGroupId(senderId);

            IdentifyResult[] result = null;
            try
            {
                // Cognitive Face Identify API で Face から Person を認識
                Guid[] guids = faces.Select(face => face.FaceId).Take(10).ToArray(); // Identify API の Face は最大 10 個まで
                result = await _faceServiceClient.IdentifyAsync(personGroupId, guids, 0.6f);
            }
            catch (FaceAPIException e)
            {
                if (e.ErrorCode == "PersonGroupNotTrained")
                {
                    // 学習が必要なら学習する
                    await _faceServiceClient.TrainPersonGroupAsync(personGroupId);
                }

                // 例外の場合は見つからなかった結果として返す
                return new Person[faces.Length];
            }

            if (result.Count() == 0)
            {
                // 結果が空の場合は見つからなかった結果として返す
                return new Person[faces.Length];
            }
            else
            {
                // Face に対応する Person の配列を生成
                var persons = new Person[faces.Length];
                for (int i = 0; i < faces.Length && i < result.Length; i++)
                {
                    if (result[i].Candidates.Count() > 0)
                    {
                        var personId = result[i].Candidates[0].PersonId.ToString();
                        var person = await _db.Person.Where(p => p.PersonGroupId == personGroupId && p.PersonId == personId).FirstOrDefaultAsync();
                        persons[i] = person;
                    }
                }

                // Person の配列を返す
                return persons;
            }
        }

        /// <summary>
        /// SenderId から <see cref="PersonGroup"/> を探す。
        /// </summary>
        /// <param name="senderId">LINE Messagin Service の SenderId</param>
        /// <returns>見つかった <see cref="PersonGroup"/> を返す。見つからない場合は null を返す。</returns>
        public async Task<PersonGroup> FindPersonGroupBySenderIdAsync(string senderId)
        {
            var personGroupId = SenderIdToPersonGroupId(senderId);
            return await _db.PersonGroup.Where(p => p.PersonGroupId == personGroupId).FirstOrDefaultAsync();
        }

        /// <summary>
        /// 名前から <see cref="Person"/> を探す。
        /// </summary>
        /// <param name="senderId">LINE Messagin Service の SenderId</param>
        /// <param name="name">名前</param>
        /// <returns>見つかった <see cref="Person"/> を返す。見つからない場合は null を返す。</returns>
        public async Task<Person> FindPersonByNameAsync(string senderId, string name)
        {
            var personGroupId = SenderIdToPersonGroupId(senderId);
            return await _db.Person.Where(p => p.PersonGroupId == personGroupId && p.Name == name).FirstOrDefaultAsync();
        }

        /// <summary>
        /// 指定の名前の <see cref="Person"/> を生成する。
        /// Cognitive Face 上とデータベース上に生成する。
        /// </summary>
        /// <param name="senderId">LINE Messagin Service の SenderId</param>
        /// <param name="name">名前</param>
        /// <returns>生成した <see cref="Person"/> を返す。</returns>
        public async Task<Person> CreatePersonAsync(string senderId, string name)
        {
            var personGroupId = SenderIdToPersonGroupId(senderId);

            // Cognitive Face 上に Person を生成
            var createPersonResult = await _faceServiceClient.CreatePersonAsync(personGroupId, name);

            // データベース上に Person を生成
            var person = new Person
            {
                PersonGroupId = personGroupId,
                PersonId = createPersonResult.PersonId.ToString(),
                Name = name,
            };
            _db.Person.Add(person);
            await _db.SaveChangesAsync();

            // Person を返す
            return person;
        }

        /// <summary>
        /// 特定の人の顔の画像を学習する。
        /// </summary>
        /// <param name="person">対象の <see cref="Person"/></param>
        /// <param name="imageStream">顔の画像</param>
        public async Task RememberPersonFaceAsync(Person person, Stream imageStream)
        {
            // Cognitive Face Person API で Person に Face を追加
            await _faceServiceClient.AddPersonFaceAsync(person.PersonGroupId, Guid.Parse(person.PersonId), imageStream);
            try
            {
                // 学習
                await _faceServiceClient.TrainPersonGroupAsync(person.PersonGroupId);
            }
            catch
            {
                // 学習のエラーは無視(学習実行中など)
            }
        }

        /// <summary>
        /// 全ての学習を忘却する。
        /// </summary>
        /// <param name="senderId">LINE Messagin Service の SenderId</param>
        public async Task ForgetAllAsync(string senderId)
        {
            var personGroupId = SenderIdToPersonGroupId(senderId);

            // 対象の PersonGroup を探す。見つからなければ何もしない。
            var personGroup = await FindPersonGroupBySenderIdAsync(senderId);
            if (personGroup == null)
            {
                return;
            }

            // Cognitive Face 上の PersonGroup を削除
            await _faceServiceClient.DeletePersonGroupAsync(personGroupId);

            // データベース上の Person と PersonGroup を削除
            _db.Person.RemoveRange(_db.Person.Where(p => p.PersonGroupId == personGroupId).ToList());
            _db.PersonGroup.Remove(personGroup);
            await _db.SaveChangesAsync();
        }
    }
}
