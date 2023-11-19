using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp78
{
    internal class Program
    {
        static readonly object LockObject = new object();
        static bool isRunning = true;
        static List<string> forbiddenWords = new List<string>();
        static string outputPath = "C:\\ForbiddenFiles";
        static string reportPath = "C:\\ForbiddenFiles\\Report.txt";

        static void Main()
        {
            Console.WriteLine("Введіть або завантажте з файлу заборонені слова:");
            string inputWords = Console.ReadLine();
            forbiddenWords = LoadForbiddenWords(inputWords);

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            Console.WriteLine("Натисніть Enter, щоб почати пошук.");
            Console.ReadLine();

            // Розпочати виконання в окремому потоці
            Task.Run(() => StartSearch());

            // Дозволяє користувачу зупинити або відновити роботу
            while (true)
            {
                Console.WriteLine("Для призупинення роботи введіть 'pause', для відновлення - 'resume', для завершення - 'stop'");
                string command = Console.ReadLine();

                if (command.ToLower() == "pause")
                {
                    isRunning = false;
                }
                else if (command.ToLower() == "resume")
                {
                    isRunning = true;
                }
                else if (command.ToLower() == "stop")
                {
                    isRunning = false;
                    break;
                }
            }

            Console.WriteLine("Робота програми завершена.");
        }

        static void StartSearch()
        {
            // Отримати всі логічні диски на комп'ютері
            string[] drives = Environment.GetLogicalDrives();

            while (isRunning)
            {
                foreach (var drive in drives)
                {
                    if (!isRunning) break;

                    string[] files = GetFilesFromDrive(drive);

                    // Пошук у файлах
                    Parallel.ForEach(files, (file) =>
                    {
                        if (!isRunning) return;

                        SearchAndCopyFile(file);
                    });
                }

                // Затримка перед наступною ітерацією
                Thread.Sleep(1000);
            }

            // Вивід звіту після завершення роботи
            GenerateReport();
        }

        static void SearchAndCopyFile(string filePath)
        {
            try
            {
                string content = File.ReadAllText(filePath);
                bool containsForbiddenWord = false;

                foreach (var word in forbiddenWords)
                {
                    if (content.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        containsForbiddenWord = true;
                        break;
                    }
                }

                if (containsForbiddenWord)
                {
                    // Створення копії файлу з іншою назвою та заміною заборонених слів на '*'
                    string newFileName = $"{outputPath}\\{Path.GetFileName(filePath)}";
                    File.Copy(filePath, newFileName);
                    string censoredContent = CensorContent(content);
                    File.WriteAllText(newFileName, censoredContent);

                    // Запис інформації в звіт
                    lock (LockObject)
                    {
                        using (StreamWriter writer = File.AppendText(reportPath))
                        {
                            writer.WriteLine($"File: {newFileName}");
                            writer.WriteLine($"Size: {new FileInfo(newFileName).Length} bytes");
                            writer.WriteLine($"Forbidden words count: {forbiddenWords.Count}");
                            writer.WriteLine("------");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
            }
        }

        static string CensorContent(string content)
        {
            foreach (var word in forbiddenWords)
            {
                content = Regex.Replace(content, $@"\b{Regex.Escape(word)}\b", "*****", RegexOptions.IgnoreCase);
            }
            return content;
        }

        static void GenerateReport()
        {
            Console.WriteLine("Генерація звіту...");
            List<string> topForbiddenWords = forbiddenWords
                .GroupBy(word => word, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .Select(group => $"{group.Key}: {group.Count()} times")
                .Take(10)
                .ToList();

            // Запис топ-10 заборонених слів в звіт
            using (StreamWriter writer = File.AppendText(reportPath))
            {
                writer.WriteLine("\nTop 10 Forbidden Words:");
                foreach (var wordInfo in topForbiddenWords)
                {
                    writer.WriteLine(wordInfo);
                }
            }

            Console.WriteLine("Звіт згенеровано: " + reportPath);
        }

        static string[] GetFilesFromDrive(string drive)
        {
            try
            {
                return Directory.GetFiles(drive, "*", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                return new string[0];
            }
        }

        static List<string> LoadForbiddenWords(string input)
        {
            List<string> words = new List<string>();

            // Перевірка, чи введено слова або вказано файл
            if (File.Exists(input))
            {
                // Завантаження слів з файлу
                words = File.ReadAllLines(input).ToList();
            }
            else
            {
                // Введення слів вручну
                words = input.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }

            return words;
        }

    }
}
