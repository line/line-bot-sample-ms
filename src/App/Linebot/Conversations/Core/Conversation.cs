using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace App.Linebot.Conversations.Core
{
    public class Conversation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MinLength(33, ErrorMessage = nameof(SenderId) + " must be 33 characters")]
        [MaxLength(33, ErrorMessage = nameof(SenderId) + " must be 33 characters")]
        public string SenderId { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = nameof(Path) + " must be between 1 and 100 characters")]
        [MaxLength(100, ErrorMessage = nameof(Path) + " must be between 1 and 100 characters")]
        public string Path { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = nameof(Data) + " must be between 1 and 10000 characters")]
        [MaxLength(10000, ErrorMessage = nameof(Data) + " must be between 1 and 10000 characters")]
        public string Data { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime? UpdatedAt { get; private set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime CreatedAt { get; private set; }
    }
}
