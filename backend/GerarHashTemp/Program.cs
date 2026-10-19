using Application.Security;

var hasher = new BCryptPasswordHasher();
Console.WriteLine(hasher.Hash("Senha@123"));