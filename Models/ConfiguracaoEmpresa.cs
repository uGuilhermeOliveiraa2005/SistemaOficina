namespace SistemaOficina.Models
{
    public class ConfiguracaoEmpresa
    {
        public int Id { get; set; }
        public string NomeEmpresa { get; set; }
        public byte[] Logo { get; set; } // Armazenaremos a imagem como um array de bytes
        public string Endereco { get; set; }
        public string Telefone { get; set; }
        public string Proprietario { get; set; }
        public string FuncionarioPrincipal { get; set; }
        public bool PrimeiroAcessoConcluido { get; set; }
        // #################### INÍCIO DA ALTERAÇÃO ####################
        public string CpfCnpj { get; set; }
        // #################### FIM DA ALTERAÇÃO ####################
    }
}