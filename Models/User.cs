namespace SistemaOficina.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; } // "Admin" ou "User"

        // #################### INÍCIO DA ALTERAÇÃO ####################
        public bool IncluirNoAcerto { get; set; }
        // #################### FIM DA ALTERAÇÃO ####################
    }
}