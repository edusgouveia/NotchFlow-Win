# Contribuindo com o NotchFlow

Obrigado por ajudar a melhorar o NotchFlow. Correções, melhorias de interface, testes e documentação são bem-vindos.

## Fluxo recomendado

1. Crie um fork do repositório.
2. Abra uma branch curta e descritiva.
3. Faça mudanças focadas, sem incluir dados pessoais ou arquivos gerados.
4. Execute:

   ```bash
   ./Scripts/security-check.sh
   swift test
   ```

5. Abra um pull request explicando o problema, a solução e como a mudança foi validada.

## Segurança e privacidade

- Nunca envie tokens, certificados, caminhos pessoais, eventos reais ou dados de reprodução.
- Não adicione telemetria, persistência ou novas integrações de rede sem atualizar `PRIVACY.md` e `SECURITY.md`.
- Vulnerabilidades devem seguir o canal privado descrito em `SECURITY.md`, e não uma issue pública detalhada.

## Licença das contribuições

Ao enviar uma contribuição, você concorda que ela será disponibilizada sob a [licença MIT](LICENSE) do projeto.
