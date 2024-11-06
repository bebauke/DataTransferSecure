using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace DataTransferSecure.Utilities
{
    public class CertUtils
    {
        public static bool IsSelfSignedCertificate(X509Certificate2 certificate)
        {
            // Überprüfe, ob der Aussteller (Issuer) und der Betreff (Subject) identisch sind
            return certificate.Issuer == certificate.Subject;
        }

        public static string GetSubject(X509Certificate2 certificate, string Entry = "CN")
        {
            var found = new List<string>();
            // Zerlege den Betreff (Subject) des Zertifikats in Tokens
            var subject = certificate.Subject;
            var strings = subject.Split(new[] { ", " }, StringSplitOptions.None);
            var searchFor = Entry + "=";

            foreach (var ele in strings)
            {

                if (ele.StartsWith(searchFor))
                {
                    // Extrahiere den Common Name und füge ihn zur Liste hinzu
                    return ele.Substring(searchFor.Length);
                }
            }
            return null;
        }
    }
}