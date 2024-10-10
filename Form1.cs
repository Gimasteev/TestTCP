using System;
using System.IO;
using HalconDotNet;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TestTCP
{
    public partial class Form1 : Form
    {
        private TcpListener tcpListener;

        public Form1()
        {
            InitializeComponent();
        }

        private async void simpleButton1_Click(object sender, EventArgs e)
        {
            await StartServerAsync(); // Запускаем сервер асинхронно
            MessageBox.Show("TCP-сервер запущен!");
        }

        private async Task StartServerAsync()
        {
            try
            {
                tcpListener = new TcpListener(IPAddress.Any, 8000);
                tcpListener.Start();
                MessageBox.Show("Сервер слушает подключения на порту 8000");

                while (true)
                {
                    TcpClient client = await tcpListener.AcceptTcpClientAsync(); // Асинхронное ожидание подключения клиента
                    _ = HandleClientCommAsync(client); // Обработка клиента в асинхронной задаче
                }
            }
            catch (Exception ex)
            {
                Invoke((MethodInvoker)delegate
                {
                    listBoxMessages.Items.Add($"Ошибка: {ex.Message}");
                });
            }
        }

        private async Task HandleClientCommAsync(TcpClient tcpClient)
        {
            using (tcpClient)
            {
                IPEndPoint clientEndPoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
                if (clientEndPoint != null)
                {
                    Invoke((MethodInvoker)delegate
                    {
                        listBoxMessages.Items.Add($"Подключен клиент: {clientEndPoint.Address}:{clientEndPoint.Port}");
                    });
                }

                NetworkStream clientStream = tcpClient.GetStream();
                byte[] message = new byte[4096];

                try
                {
                    int bytesRead;
                    while ((bytesRead = await clientStream.ReadAsync(message, 0, message.Length)) != 0)
                    {
                        string clientMessage = Encoding.UTF8.GetString(message, 0, bytesRead);

                        // Добавим отладочное сообщение для проверки пути
                        Invoke((MethodInvoker)delegate
                        {
                            listBoxMessages.Items.Add($"Проверка пути: {clientMessage}");
                        });

                        if (File.Exists(clientMessage))
                        {
                            // Файл найден
                            Invoke((MethodInvoker)delegate
                            {
                                listBoxMessages.Items.Add($"Файл найден: {clientMessage}");
                            });

                            string response = AnalyzeImage(clientMessage);

                            byte[] buffer = Encoding.UTF8.GetBytes(response);
                            await clientStream.WriteAsync(buffer, 0, buffer.Length);
                        }
                        else
                        {
                            // Файл не найден
                            Invoke((MethodInvoker)delegate
                            {
                                listBoxMessages.Items.Add($"Ошибка: Файл не найден по пути: {clientMessage}");
                            });

                            byte[] buffer = Encoding.UTF8.GetBytes("Ошибка: Файл не найден");
                            await clientStream.WriteAsync(buffer, 0, buffer.Length);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Invoke((MethodInvoker)delegate
                    {
                        listBoxMessages.Items.Add($"Ошибка обработки клиента: {ex.Message}");
                    });
                }
            }
        }







        private TcpClient client;
        private NetworkStream clientStream;

        private async void simpleButton2_Click(object sender, EventArgs e)
        {
            // Подключение к серверу
            try
            {
                client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", 8000); // Асинхронное подключение к серверу
                clientStream = client.GetStream();
                MessageBox.Show("TCP клиент подключен к серверу!");
            }
            catch (SocketException ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка: {ex.Message}");
            }
        }

        private async void buttonSend_Click(object sender, EventArgs e)
        {
            if (client == null || !client.Connected)
            {
                MessageBox.Show("Клиент не подключен к серверу. Пожалуйста, сначала подключитесь.");
                return;
            }

            string messageToSend = textBoxMessage.Text; // Используем textBoxMessage для ввода сообщения

            // Проверяем, выбран ли файл и существует ли он
            if (string.IsNullOrEmpty(messageToSend) || !File.Exists(messageToSend))
            {
                MessageBox.Show("Пожалуйста, выберите существующий файл для отправки.");
                return;
            }

            try
            {
                // Отправляем сообщение на сервер с использованием UTF-8
                byte[] buffer = Encoding.UTF8.GetBytes(messageToSend);
                await clientStream.WriteAsync(buffer, 0, buffer.Length); // Асинхронная отправка сообщения

                // Читаем ответ от сервера
                byte[] responseBuffer = new byte[4096];
                int bytesRead = await clientStream.ReadAsync(responseBuffer, 0, responseBuffer.Length); // Асинхронное чтение ответа
                string responseMessage = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);

                MessageBox.Show($"Ответ от сервера: {responseMessage}");
            }
            catch (SocketException ex)
            {
                MessageBox.Show($"Ошибка передачи данных: {ex.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка: {ex.Message}");
            }
        }


        

        private void buttonSelectFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Изображения (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp";
                openFileDialog.Title = "Выберите изображение";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;
                    textBoxMessage.Text = filePath; // Отобразим выбранный путь в текстовом поле для отправки
                }
            }
        }

        private string AnalyzeImage(string imagePath)
        {
            try
            {
                // Загружаем изображение
                HObject image;
                HOperatorSet.ReadImage(out image, imagePath);

                // Преобразуем изображение в градации серого
                HOperatorSet.Rgb1ToGray(image, out image);
                listBoxMessages.Items.Add("Преобразовано изображение в градации серого.");

                // Применяем сглаживание для уменьшения шума
                HOperatorSet.MedianImage(image, out image, "circle", 3, "mirrored");
                listBoxMessages.Items.Add("Применено сглаживание.");

                // Применяем пороговое значение (Threshold)
                HObject thresholdedRegions;
                HOperatorSet.Threshold(image, out thresholdedRegions, 100, 200);
                listBoxMessages.Items.Add("Применена пороговая фильтрация.");

                // Применяем морфологическое открытие для удаления мелких шумов
                HOperatorSet.OpeningCircle(thresholdedRegions, out thresholdedRegions, 3.5);
                listBoxMessages.Items.Add("Применено морфологическое открытие.");

                // Применяем морфологическое закрытие для соединения разорванных областей
                HOperatorSet.ClosingCircle(thresholdedRegions, out thresholdedRegions, 3.5);
                listBoxMessages.Items.Add("Применено морфологическое закрытие.");

                // Находим соединенные компоненты
                HObject connectedRegions;
                HOperatorSet.Connection(thresholdedRegions, out connectedRegions);
                listBoxMessages.Items.Add("Найдены соединенные компоненты.");

                // Фильтруем объекты по площади, чтобы убрать шум и маленькие элементы
                HObject selectedRegions;
                HOperatorSet.SelectShape(connectedRegions, out selectedRegions, "area", "and", 500, 10000);
                listBoxMessages.Items.Add("Произведена фильтрация по площади объектов.");

                // Подсчитываем количество объектов
                HTuple numberOfObjects;
                HOperatorSet.CountObj(selectedRegions, out numberOfObjects);

                // Отображаем количество объектов в ListBox
                listBoxMessages.Items.Add($"Количество объектов на изображении: {numberOfObjects}");

                // Освобождаем ресурсы
                image.Dispose();
                thresholdedRegions.Dispose();
                connectedRegions.Dispose();
                selectedRegions.Dispose();

                // Возвращаем результат анализа
                return numberOfObjects == 19 ? "OK" : "NG";
            }
            catch (Exception ex)
            {
                listBoxMessages.Items.Add($"Ошибка анализа изображения: {ex.Message}");
                return "Ошибка анализа изображения";
            }
        }

























    }
}
