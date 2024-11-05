### DataTransferSecure

This is a simple WinForms tool to transfer data (currently text only) between two computers securely. 
It uses AES encryption to encrypt the data before sending it over the network. 
The data is encrypted using a key that is generated using the Diffie-Hellman key exchange algorithm. 

The main three pillars of security are confidentiality, integrity, and authenticity. Currently the tool only provides confidentiality. 
TODO: -> Integrity and authenticity should be added: Use SHA-256 to hash the data and sign it with a private key. The public key can be used to verify the signature.

The tool is written in C# and uses the .NET framework.