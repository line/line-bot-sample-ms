using Microsoft.ProjectOxford.Face.Contract;
using System.IO;
using System.Threading.Tasks;

namespace App.Linebot.Persons
{
    /// <summary>
    /// グループと人を管理する。顔を認識して人と結びつける。
    /// </summary>
    public interface IPersonService
    {
        Task<PersonGroup> EnsurePersonGroupAsync(string senderId);

        Task<Face[]> DetectFaceAsync(Stream imageStream);

        Task<Person> FindPersonByFaceAsync(string senderId, Face face);

        Task<Person[]> FindPersonByFacesAsync(string senderId, Face[] face);

        Task<PersonGroup> FindPersonGroupBySenderIdAsync(string senderId);

        Task<Person> FindPersonByNameAsync(string senderId, string name);

        Task<Person> CreatePersonAsync(string senderId, string name);

        Task RememberPersonFaceAsync(Person person, Stream imageStream);

        Task ForgetAllAsync(string senderId);
    }
}
