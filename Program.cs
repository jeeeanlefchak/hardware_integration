using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using RJCP.IO.Ports;

class Program
{
    static string NomeLog = $"log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";

    static void Main()
    {
        LogInfo("CARREGANDO CONFIG");
        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        if (!File.Exists(configPath))
        {
            LogErro("Arquivo de configuração não encontrado.");
            return;
        }

        string json = File.ReadAllText(configPath);
        LogInfo("CONFIG CARREGADA: " + json);

        try
        {
            BalancaRequest balanca = JsonSerializer.Deserialize<BalancaRequest>(json);
            SerialService service = new SerialService(NomeLog);
            var response = service.Ler(balanca);

            if (response.RetornoBalanca != null)
                LogInfo("Leitura da balança: " + response.RetornoBalanca);
            else
                LogErro("Erro: " + response.MensagemErro);
        }
        catch (Exception ex)
        {
            LogErro("Erro ao carregar configuração: " + ex.Message);
        }
    }

    static void LogErro(string mensagem) => File.AppendAllText(NomeLog, $"{DateTime.Now} - ERRO - {mensagem}\n");

    static void LogInfo(string mensagem) => File.AppendAllText(NomeLog, $"{DateTime.Now} - INFO - {mensagem}\n");
}

public class SerialService
{
    private string NomeLog;

    public SerialService(string logFileName)
    {
        NomeLog = logFileName;
    }

    public BalancaResponse Ler(BalancaRequest balanca)
    {
        LogInfo("INICIANDO LEITURA DA BALANÇA");

        using (var porta = new SerialPortStream())
        {
            try
            {
                PreencherDadosPorta(porta, balanca);
                LogInfo($"ABRINDO PORTA {porta.PortName}");

                porta.Open();
                LogInfo("PORTA ABERTA");

                if (balanca.SemAutoPrint)
                    ProcessarComando(porta, balanca.Comando, balanca.TempoEsperaLeitura);

                LogInfo("LENDO PESO DA BALANÇA");
                porta.Flush();

                string retornoBalanca = LerLinha(porta);
                LogInfo("DADO RECEBIDO: " + retornoBalanca);


                List<string> retornoBalanca2 = LerLinhas(porta, 100);
                LogInfo("DADO RECEBIDO2: " + retornoBalanca2.ToString());

                return new BalancaResponse(retornoBalanca, string.Empty);
            }
            catch (UnauthorizedAccessException ex) // Falta de permissão para acessar a porta
            {
                LogErro("Acesso negado à porta COM. Execute como administrador. " + ex.Message);
                return new BalancaResponse(null, "Acesso negado à porta COM. Verifique as permissões.");
            }
            catch (IOException ex) // Problema físico ou erro de comunicação
            {
                LogErro("Erro de I/O na comunicação com a balança: " + ex.Message);
                return new BalancaResponse(null, "Erro de comunicação com a balança. Verifique os cabos e a conexão.");
            }
            catch (ArgumentException ex) // Configuração inválida da porta
            {
                LogErro("Configuração inválida da porta COM: " + ex.Message);
                return new BalancaResponse(null, "Configuração da porta COM inválida. Verifique os parâmetros.");
            }
            catch (InvalidOperationException ex) // Tentando abrir uma porta já aberta
            {
                LogErro("Tentativa de abrir uma porta já em uso: " + ex.Message);
                return new BalancaResponse(null, "A porta COM já está em uso por outro programa.");
            }
            catch (TimeoutException ex) // Resposta demorou muito
            {
                LogErro("Tempo limite atingido ao ler da balança: " + ex.Message);
                return new BalancaResponse(null, "A balança não respondeu a tempo. Verifique a conexão.");
            }
            catch (Exception ex) // Erro genérico para capturar qualquer outro problema
            {
                LogErro("Erro inesperado na comunicação: " + ex.Message);
                return new BalancaResponse(null, "Erro desconhecido. Detalhes: " + ex.Message);
            }
        }
    }

    private string LerLinha(SerialPortStream porta)
    {
        using (var reader = new StreamReader(porta))
        {
            return reader.ReadLine();
        }
    }

    private List<string> LerLinhas(SerialPortStream porta, int quantidade)
    {
        List<string> linhas = new List<string>();

        using (var reader = new StreamReader(porta))
        {
            for (int i = 0; i < quantidade; i++)
            {
                string linha = reader.ReadLine();
                if (!string.IsNullOrEmpty(linha))
                    linhas.Add(linha);
            }
        }
        return linhas;
    }

    private void PreencherDadosPorta(SerialPortStream porta, BalancaRequest balanca)
    {
        porta.PortName = balanca.Porta;
        porta.BaudRate = balanca.Velocidade;
        porta.DataBits = balanca.BitsDados;
        porta.Parity = balanca.Paridade;
        porta.StopBits = balanca.BitsParada;
        porta.ReadTimeout = 10000;
    }

    private void ProcessarComando(SerialPortStream porta, string comando, int tempoEspera)
    {
        porta.WriteLine(comando);
        System.Threading.Thread.Sleep(tempoEspera);
    }

    private void LogErro(string mensagem) => File.AppendAllText(NomeLog, $"{DateTime.Now} - ERRO - {mensagem}\n");

    private void LogInfo(string mensagem) => File.AppendAllText(NomeLog, $"{DateTime.Now} - INFO - {mensagem}\n");
}

public class BalancaResponse
{
    public string RetornoBalanca { get; }
    public string MensagemErro { get; }

    public BalancaResponse(string retornoBalanca, string mensagemErro)
    {
        RetornoBalanca = retornoBalanca;
        MensagemErro = mensagemErro;
    }
}

public class BalancaRequest
{
    [JsonPropertyName("modelo")]
    public string Modelo { get; set; }

    [JsonPropertyName("descricao")]
    public string Descricao { get; set; }

    [JsonPropertyName("bitsDados")]
    public int BitsDados { get; set; }

    [JsonPropertyName("paridade")]
    public Parity Paridade { get; set; }

    [JsonPropertyName("bitsParada")]
    public StopBits BitsParada { get; set; }

    [JsonPropertyName("porta")]
    public string Porta { get; set; }

    [JsonPropertyName("velocidade")]
    public int Velocidade { get; set; }

    [JsonPropertyName("semAutoPrint")]
    public bool SemAutoPrint { get; set; }

    [JsonPropertyName("tempoEsperaLeitura")]
    public int TempoEsperaLeitura { get; set; }

    [JsonPropertyName("comando")]
    public string Comando { get; set; }
}
