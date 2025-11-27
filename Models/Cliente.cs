namespace SistemaOficina.Models
{
    public class Cliente
    {
        public int Id { get; set; }
        public string Nome { get; set; }
        public string Endereco { get; set; }
        public string Cidade { get; set; }
        public string Bairro { get; set; }
        public string Telefone1 { get; set; }
        public string Telefone2 { get; set; }
        // #################### INÍCIO DA ALTERAÇÃO ####################
        public string CpfCnpj { get; set; }
        // #################### FIM DA ALTERAÇÃO ####################
    }
}