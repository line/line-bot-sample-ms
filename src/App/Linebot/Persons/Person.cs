using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace App.Linebot.Persons
{
    public class Person
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = nameof(PersonGroupId) + " must be between 1 and 64 characters")]
        [MaxLength(64, ErrorMessage = nameof(PersonGroupId) + " must be between 1 and 64 characters")]
        public string PersonGroupId { get; set; }

        [Required]
        [MinLength(36, ErrorMessage = nameof(PersonId) + " must be 36 characters")]
        [MaxLength(36, ErrorMessage = nameof(PersonId) + " must be 36 characters")]
        public string PersonId { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = nameof(Name) + " must be between 1 and 128 characters")]
        [MaxLength(128, ErrorMessage = nameof(Name) + " must be between 1 and 128 characters")]
        public string Name { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime? UpdatedAt { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime CreatedAt { get; set; }
    }
}
