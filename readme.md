# DataTransferSecure

*This is an educational project and does not claim to meet high-security requirements. It is intended solely for learning purposes, and no guarantees are made regarding its suitability for secure data transmission in real-world applications.*

## Overview

DataTransferSecure is a simple WinForms tool written in C# using the .NET framework. It allows for the secure transfer of data (currently text only) between two computers over a network.

## Features

- **Secure Data Transfer**: Utilizes AES encryption to encrypt data before transmission, ensuring confidentiality.
- **Key Exchange Mechanism**: Implements the Diffie-Hellman key exchange algorithm to securely generate a shared encryption key between the sender and receiver.
- **Certificate Support**: Allows loading and verification of X.509 certificates to enhance authenticity.
- **Integrity Verification**: Optionally uses checksums to verify data integrity during transmission.
- **Configurable Settings**: Provides a user-friendly settings interface to customize ports, encryption options, and certificate details.

## Security Principles

The tool is designed around the three main pillars of security:

1. **Confidentiality**: Ensures that the data is accessible only to those authorized to have access. Achieved through AES encryption.
2. **Integrity**: Guarantees that the data has not been altered during transmission. Achieved through optional checksums.
3. **Authenticity**: Verifies the identities of the communicating parties. Achieved through the use of X.509 certificates.

## Getting Started

### Prerequisites

- Windows operating system
- .NET Framework installed
- Visual Studio (for building the project)

### Installation

1. **Clone the Repository**:

   ```bash
   git clone https://github.com/bebauke/DataTransferSecure.git
   ```

2. **Open the Solution**:

   Open `DataTransferSecure.sln` in Visual Studio.

3. **Build the Project**:

   Build the solution to restore dependencies and compile the code.

### Usage

1. **Launch the Application**:

   Run the application executable on both the sending and receiving computers.

2. **Configure Settings**:

   - Click on the **Settings** button to open the configuration window.
   - Set the **Local UDP Port**, **Default UDP Server Port**, and **TCP Server Port** according to your network setup.
   - Choose your desired encryption technologies:
     - **AES Encryption**
     - **X.509 Certificates** (provide the certificate path and password)
     - **Checksum Verification**

3. **Establish Connection**:

   Use `File>Settings` to establish a connection between the two computers.

4. **Send and Receive Messages**:

   Begin sending and receiving text messages securely.

## Screenshots

![grafik](https://github.com/user-attachments/assets/bc345e35-7c07-4b93-955a-e27264607f1c)


## Limitations

- Currently supports text data only.
- Not intended for deployment in production environments.
- Does not guarantee protection against advanced security threats.

## Contribution

No contributions for now

## Disclaimer

This is an educational project and does not claim to meet high-security requirements. It is intended solely for learning purposes, and **no guarantees are made regarding its suitability for secure data transmission in real-world applications**.
