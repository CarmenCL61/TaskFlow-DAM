using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UD03_TaskFlowDAM.Data;
using UD03_TaskFlowDAM.Models;

namespace UD03_TaskFlowDAM
{
    /// <summary>
    /// Lógica de interacción para MainWindow.xaml.
    /// Implementa Data Binding directo mediante DataContext y gestiona la persistencia
    /// automática en SQLite utilizando Entity Framework Core (TaskFlowDbContext).
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly TaskFlowDbContext _context;

        // Propiedades enlazadas del formulario de inserción
        private string _nuevoTitulo = string.Empty;
        public string NuevoTitulo
        {
            get => _nuevoTitulo;
            set
            {
                if (_nuevoTitulo != value)
                {
                    _nuevoTitulo = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PuedeAgregar));
                    //Avisa a WPF de que debe volver a comprobar si el botón puede habilitarse cada vez que cambia el título

                }
            }
        }

        //Devuelve false si el titulo esta vacío o contiene solo espacios
        public bool PuedeAgregar => !string.IsNullOrWhiteSpace(NuevoTitulo);

        private string _nuevoModulo = "DI";
        public string NuevoModulo
        {
            get => _nuevoModulo;
            set
            {
                if (_nuevoModulo != value)
                {
                    _nuevoModulo = value;
                    OnPropertyChanged();
                }
            }
        }

        private DateTime? _nuevaFechaLimite;
        public DateTime? NuevaFechaLimite
        {
            get => _nuevaFechaLimite;
            set
            {
                if (_nuevaFechaLimite != value)
                {
                    _nuevaFechaLimite = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _nuevaPrioridad = Prioridad.Media;
        public string NuevaPrioridad
        {
            get => _nuevaPrioridad;
            set
            {
                if (_nuevaPrioridad != value)
                {
                    _nuevaPrioridad = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _nuevaEsUrgente;
        public bool NuevaEsUrgente
        {
            get => _nuevaEsUrgente;
            set
            {
                if (_nuevaEsUrgente != value)
                {
                    _nuevaEsUrgente = value;
                    OnPropertyChanged();
                }
            }
        }

        // Propiedades reactivas para métricas y estado
        private string _estadoApp = "Listo";

        public string EstadoApp
        {
            get => _estadoApp;
            set
            {
                if (_estadoApp != value)
                {
                    _estadoApp = value;
                    OnPropertyChanged();
                }
            }
        }

        public int TareasCompletadasCount => //Cuenta las tareas completadas en la colección observable
            Tareas.Count(t => t.Completada);
        public int TareasPendientesCount => //Cuenta las tareas pendientes en la colección observable
            Tareas.Count(t=> !t.Completada);
        public double PorcentajeProgreso => //Calcula el porcentaje completado. 
            Tareas.Count == 0
            ?0
            : (double)TareasCompletadasCount / Tareas.Count * 100;

        public string TextoProgreso =>
        $"{TareasCompletadasCount} de {Tareas.Count} tareas completadas " +
        $"({PorcentajeProgreso:0}%)";

        public ObservableCollection<Tarea> Tareas { get; } = new();


        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _context = new TaskFlowDbContext();
            _context.InicializarBaseDatos();

            
            Tareas.CollectionChanged += Tareas_CollectionChanged;
            CargarTareasDesdeSqlite();
        }


        //Metodos auxiliares para optimizar el scroll en la lista de tareas 
        private void ListaTareas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is DependencyObject elemento)
            {
                var scrollViewer = FindParentScrollViewer(elemento);

                if (scrollViewer != null)
                {
                    scrollViewer.ScrollToVerticalOffset(
                        scrollViewer.VerticalOffset - e.Delta);

                    e.Handled = true;
                }
            }
        }

        private static ScrollViewer? FindParentScrollViewer(DependencyObject elemento)
        {
            DependencyObject? actual = VisualTreeHelper.GetParent(elemento);

            while (actual != null)
            {
                if (actual is ScrollViewer scrollViewer)
                    return scrollViewer;

                actual = VisualTreeHelper.GetParent(actual);
            }

            return null;
        }





        /// <summary>
        /// Recupera las tareas desde SQLite a través de EF Core y las suscribe a la colección observable.
        /// </summary>
        private void CargarTareasDesdeSqlite()
        {
            var listaTareas = _context.Tareas.OrderBy(t => t.Completada).ThenByDescending(t => t.Id).ToList();
            Tareas.Clear();

            foreach (var tarea in listaTareas)
            {
                Tareas.Add(tarea);
            }  
        }

        private void Tareas_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if(e.OldItems != null)
            {
                foreach (Tarea tarea in e.OldItems)
                {
                    tarea.PropertyChanged -= Tarea_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (Tarea tarea in e.NewItems)
                {
                    tarea.PropertyChanged += Tarea_PropertyChanged;
                }
            }

            ActualizarMetricas();
        }

        private void Tarea_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Tarea.Completada) ||
                string.IsNullOrEmpty(e.PropertyName))
            {
                ActualizarMetricas();
                if(sender is Tarea tarea)
                {
                    _context.SaveChanges();//Persiste los cambios en SQLite 
                }
            }
        }

        private void ActualizarMetricas()
        {
            OnPropertyChanged(nameof(TareasCompletadasCount));
            OnPropertyChanged(nameof(TareasPendientesCount));
            OnPropertyChanged(nameof(PorcentajeProgreso));
            OnPropertyChanged(nameof(TextoProgreso));
        }

        /// <summary>
        /// Añade una nueva tarea a la base de datos SQLite y a la colección reactiva.
        /// Activado por clic o pulsando la tecla Enter gracias a IsDefault="True".
        /// </summary>
        private void BtnAgregar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NuevoTitulo))
            {
                MessageBox.Show("Por favor, introduzca un título para la tarea.", "Campo Obligatorio",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var nuevaTarea = new Tarea
            {
                Titulo = NuevoTitulo.Trim(),//Elimina espacios sobrantes
                Modulo = NuevoModulo,
                FechaLimite = NuevaFechaLimite,
                Prioridad = NuevaPrioridad,
                EsUrgente = NuevaEsUrgente,
                Completada = false
            };

            // 1. Guardar en SQLite mediante Entity Framework Core
            _context.Tareas.Add(nuevaTarea);
            _context.SaveChanges();

            // 2. Suscribir y añadir a la ObservableCollection para reflejar en XAML
            Tareas.Insert(0, nuevaTarea);

            // 3. Limpiar formulario y notificar 
            BtnLimpiar_Click(sender, e);
            EstadoApp = "Tarea agregada correctamente";
        }

        /// <summary>
        /// Restablece los campos del formulario de entrada.
        /// Activado por clic o pulsando la tecla Escape gracias a IsCancel="True".
        /// </summary>
        private void BtnLimpiar_Click(object sender, RoutedEventArgs e)
        {
            NuevoTitulo = string.Empty;
            NuevoModulo = "DI";
            NuevaFechaLimite = null;
            NuevaPrioridad = Prioridad.Media;
            NuevaEsUrgente = false;

            EstadoApp = "Formulario limpio";
        }

        /// <summary>
        /// Elimina una tarea individual de SQLite y del listado enlazado.
        /// </summary>
        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Tarea tarea)
            {
                var resultado = MessageBox.Show(
                    $"¿Desea eliminar la tarea '{tarea.Titulo}' de la base de datos?",
                    "Confirmar Eliminación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (resultado == MessageBoxResult.Yes)
                {
                    _context.Tareas.Remove(tarea);
                    _context.SaveChanges();
                    Tareas.Remove(tarea);

                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _context.Dispose();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}