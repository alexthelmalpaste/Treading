using System;
using System.Collections.Generic;
using System.Threading;

namespace SleepingBarber
{
    class Customer
    {
        public int Id { get; }
        public ManualResetEventSlim Done { get; } = new ManualResetEventSlim(false);

        public Customer(int id) => Id = id;
    }

    class BarberShop
    {
        private readonly int waitingRoomSize;
        private readonly Queue<Customer> waitingRoom = new Queue<Customer>();
        private readonly object locker = new object();

        // Семафор: скільки клієнтів доступно для роботи перукаря
        private readonly SemaphoreSlim customersAvailable = new SemaphoreSlim(0);

        // Для красивого логування часу
        private readonly Random rnd = new Random();

        public BarberShop(int waitingRoomSize)
        {
            this.waitingRoomSize = waitingRoomSize;
        }

        public void StartBarber()
        {
            var barberThread = new Thread(BarberWork)
            {
                IsBackground = true,
                Name = "Barber"
            };
            barberThread.Start();
        }

        private void BarberWork()
        {
            while (true)
            {
                // Засинає, поки немає клієнтів
                customersAvailable.Wait(); // прокидається, коли клієнт прибув

                Customer customer;
                lock (locker)
                {
                    // Безпечне зняття клієнта з черги
                    customer = waitingRoom.Dequeue();
                }

                Log($"Перукар стриже клієнта #{customer.Id}");
                // Час стрижки
                Thread.Sleep(rnd.Next(800, 2000));
                Log($"Клієнт #{customer.Id} пострижений");

                // Сигнал конкретному клієнту, що його стрижку завершено
                customer.Done.Set();
            }
        }

        public void ArriveCustomer(int id)
        {
            var customer = new Customer(id);

            lock (locker)
            {
                if (waitingRoom.Count >= waitingRoomSize)
                {
                    Log($"Клієнт #{id} не знайшов місця у приймальні і пішов");
                    return;
                }

                waitingRoom.Enqueue(customer);
                Log($"Клієнт #{id} сів у приймальні (зайнято: {waitingRoom.Count}/{waitingRoomSize})");
            }

            // Розбудити/сповістити перукаря, що є робота
            customersAvailable.Release();

            // Чекати завершення власної стрижки
            customer.Done.Wait();
            Log($"Клієнт #{id} виходить з перукарні");
        }

        private static void Log(string msg)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var shop = new BarberShop(waitingRoomSize: 3);
            shop.StartBarber();

            // Генерація клієнтів у різний час
            var rnd = new Random();
            var threads = new List<Thread>();

            for (int i = 1; i <= 15; i++)
            {
                int id = i;
                var t = new Thread(() =>
                {
                    // Прихід клієнта з випадковою затримкою
                    Thread.Sleep(rnd.Next(200, 1000));
                    shop.ArriveCustomer(id);
                })
                {
                    Name = $"Customer-{id}"
                };
                threads.Add(t);
                t.Start();
            }

            // Дочекатися всіх клієнтів
            foreach (var t in threads)
                t.Join();

            Console.WriteLine("Симуляцію завершено. Натисніть Enter для виходу.");
            Console.ReadLine();
        }
    }
}
