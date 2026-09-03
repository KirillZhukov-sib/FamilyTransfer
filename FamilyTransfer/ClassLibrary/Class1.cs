/*< Window x: Class = "FamilyTransfer.Views.ExportWindow"
        xmlns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns: x = "http://schemas.microsoft.com/winfx/2006/xaml"
        Title = "Экспорт в MySQL"
        Height = "750" Width = "850"
        WindowStartupLocation = "CenterScreen"
        ResizeMode = "CanResize"
        MinHeight = "900" MinWidth = "750" >

    < Window.Resources >
        < Style TargetType = "CheckBox" >
            < Setter Property = "FontSize" Value = "12" />
            < Setter Property = "Margin" Value = "2,2" />
            < Setter Property = "VerticalContentAlignment" Value = "Center" />
        </ Style >

        < Style TargetType = "ListBox" >
            < Setter Property = "FontSize" Value = "12" />
            < Setter Property = "Margin" Value = "2,2" />
            < Setter Property = "BorderThickness" Value = "1" />
            < Setter Property = "BorderBrush" Value = "#DDD" />
            < Setter Property = "Background" Value = "#F9F9F9" />
        </ Style >

        < Style TargetType = "ListBoxItem" >
            < Setter Property = "Padding" Value = "5,3" />
            < Setter Property = "Margin" Value = "0,1" />
        </ Style >
    </ Window.Resources >

    < Grid Margin = "10" >
        < Grid.RowDefinitions >
            < RowDefinition Height = "Auto" />
            < RowDefinition Height = "110" />
            < RowDefinition Height = "Auto" />
            < RowDefinition Height = "Auto" />
            < RowDefinition Height = "Auto" />
            < RowDefinition Height = "Auto" />
            < RowDefinition />
            < RowDefinition Height = "Auto" />
        </ Grid.RowDefinitions >

        < Grid.ColumnDefinitions >
            < ColumnDefinition Width = "*" />
            < ColumnDefinition Width = "Auto" />
        </ Grid.ColumnDefinitions >

        < !--Заголовок-- >
        < Border Grid.Row = "0" Grid.ColumnSpan = "2" Background = "#2C3E50" CornerRadius = "5" Margin = "0,0,0,10" Padding = "10" >
            < TextBlock Text = "{Binding DocumentTitle}"
                       FontSize = "16"
                       FontWeight = "Bold"
                       Foreground = "White"
                       HorizontalAlignment = "Center"
                       TextWrapping = "Wrap" />
        </ Border >

        < !--Информация-- >
        < GroupBox Grid.Row = "1" Grid.ColumnSpan = "2" Header = "Информация" Margin = "0,0,0,5" >
            < Grid Margin = "5" >
                < Grid.RowDefinitions >
                    < RowDefinition Height = "Auto" />
                    < RowDefinition Height = "Auto" />
                    < RowDefinition Height = "Auto" />
                    < RowDefinition Height = "Auto" />
                </ Grid.RowDefinitions >

                < TextBlock Grid.Row = "0" Text = "{Binding StatusMessage}" FontWeight = "Bold" Margin = "0,0,0,5" TextWrapping = "Wrap" />
                < TextBlock Grid.Row = "1" Text = "{Binding ProgressDetail}" FontSize = "11" Foreground = "Gray" Margin = "0,0,0,3" TextWrapping = "Wrap" />
                < TextBlock Grid.Row = "2" Text = "{Binding SpeedInfo}" FontSize = "11" Foreground = "Blue" Margin = "0,0,0,3" TextWrapping = "Wrap" />
                < TextBlock Grid.Row = "3" Text = "{Binding TimeInfo}" FontSize = "11" Foreground = "Green" TextWrapping = "Wrap" />
            </ Grid >
        </ GroupBox >

        < !--Настройки подключения-- >
        < GroupBox Grid.Row = "2" Grid.ColumnSpan = "2" Header = "Настройки подключения" Margin = "0,0,0,5" >
            < Grid Margin = "5" >
                < Grid.ColumnDefinitions >
                    < ColumnDefinition Width = "100" />
                    < ColumnDefinition Width = "*" />
                </ Grid.ColumnDefinitions >

                < TextBlock Grid.Column = "0" Text = "База данных:" VerticalAlignment = "Center" FontWeight = "Normal" />
                < TextBox Grid.Column = "1" Text = "{Binding DatabaseName, UpdateSourceTrigger=PropertyChanged}"
                         Margin = "5,0,0,0" />
            </ Grid >
        </ GroupBox >

        < !--Подгруженные связи-- >
        < GroupBox Grid.Row = "3" Grid.ColumnSpan = "2" Header = "Подгруженные связи" Margin = "0,0,0,5" >
            < Grid Margin = "5" >
                < Grid.RowDefinitions >
                    < RowDefinition Height = "Auto" />
                    < RowDefinition Height = "Auto" />
                </ Grid.RowDefinitions >

                < TextBlock Grid.Row = "0" Text = "{Binding LinksCountInfo}"
                           FontSize = "11"
                           Foreground = "Gray"
                           Margin = "0,0,0,5" />

                < ScrollViewer Grid.Row = "1" VerticalScrollBarVisibility = "Auto" MaxHeight = "100" >
                    < ListBox ItemsSource = "{Binding LoadedLinks}"
                             BorderThickness = "0"
                             Background = "Transparent" >
                        < ListBox.ItemTemplate >
                            < DataTemplate >
                                < Border BorderBrush = "#E0E0E0"
                                        BorderThickness = "0,0,0,1"
                                        Padding = "5,3" >
                                    < TextBlock Text = "{Binding}"
                                               FontSize = "12"
                                               Foreground = "#2C3E50"
                                               Margin = "5,0,0,0" />
                                </ Border >
                            </ DataTemplate >
                        </ ListBox.ItemTemplate >
                    </ ListBox >
                </ ScrollViewer >
            </ Grid >
        </ GroupBox >

        < !--НОВЫЙ РЯД: Настройки экспорта -->
        < GroupBox Grid.Row = "4" Grid.ColumnSpan = "2" Header = "Настройки экспорта" Margin = "0,0,0,5" >
            < Grid Margin = "5" >
                < CheckBox IsChecked = "{Binding IncludeLinkedModels}"
                          Content = "Экспортировать из связанных моделей"
                          FontSize = "12"
                          Margin = "5,5,0,5" />
            </ Grid >
        </ GroupBox >

        < !--Выбор категорий-- >
        < GroupBox Grid.Row = "5" Grid.ColumnSpan = "2" Header = "Выбор категорий для экспорта" Margin = "0,0,0,5" >
            < Grid Margin = "5" >
                < Grid.ColumnDefinitions >
                    < ColumnDefinition Width = "*" />
                    < ColumnDefinition Width = "*" />
                </ Grid.ColumnDefinitions >

                < StackPanel Grid.Column = "0" Margin = "0,0,10,0" >
                    < TextBlock Text = "Трубопроводные системы" FontWeight = "Bold" Margin = "0,0,0,5" />

                    < CheckBox IsChecked = "{Binding IncludePipeAccessory}"
                              Content = "Pipe Accessory" />
                    < CheckBox IsChecked = "{Binding IncludePipeFitting}"
                              Content = "Pipe Fitting" />
                    < CheckBox IsChecked = "{Binding IncludePlumbingFixtures}"
                              Content = "Plumbing Fixtures" />
                    < CheckBox IsChecked = "{Binding IncludePipeCurves}"
                              Content = "Pipe Curves" />
                    < CheckBox IsChecked = "{Binding IncludePipeInsulations}"
                              Content = "Pipe Insulations" />

                    < StackPanel Orientation = "Horizontal" Margin = "0,10,0,0" >
                        < Button Content = "Выбрать все трубы"
                                Command = "{Binding SelectAllPipesCommand}"
                                FontSize = "10" Height = "25" Width = "120"
                                Background = "#3498DB" Foreground = "White" />
                        < Button Content = "Снять все"
                                Command = "{Binding DeselectAllPipesCommand}"
                                FontSize = "10" Height = "25" Width = "100"
                                Background = "#95A5A6" Foreground = "White"
                                Margin = "5,0,0,0" />
                    </ StackPanel >
                </ StackPanel >

                < StackPanel Grid.Column = "1" Margin = "10,0,0,0" >
                    < TextBlock Text = "Вентиляционные системы" FontWeight = "Bold" Margin = "0,0,0,5" />

                    < CheckBox IsChecked = "{Binding IncludeDuctCurves}"
                              Content = "Duct Curves" />
                    < CheckBox IsChecked = "{Binding IncludeMechanicalEquipment}"
                              Content = "Mechanical Equipment" />
                    < CheckBox IsChecked = "{Binding IncludeDuctTerminal}"
                              Content = "Duct Terminal" />
                    < CheckBox IsChecked = "{Binding IncludeDuctAccessory}"
                              Content = "Duct Accessory" />
                    < CheckBox IsChecked = "{Binding IncludeDuctInsulations}"
                              Content = "Duct Insulations" />
                    < CheckBox IsChecked = "{Binding IncludeDuctFitting}"
                              Content = "Duct Fitting" />

                    < StackPanel Orientation = "Horizontal" Margin = "0,10,0,0" >
                        < Button Content = "Выбрать все воздуховоды"
                                Command = "{Binding SelectAllDuctsCommand}"
                                FontSize = "10" Height = "25" Width = "140"
                                Background = "#3498DB" Foreground = "White" />
                        < Button Content = "Снять все"
                                Command = "{Binding DeselectAllDuctsCommand}"
                                FontSize = "10" Height = "25" Width = "100"
                                Background = "#95A5A6" Foreground = "White"
                                Margin = "5,0,0,0" />
                    </ StackPanel >
                </ StackPanel >
            </ Grid >
        </ GroupBox >

        < !--Кнопки и Лог -->
        <Grid Grid.Row="6" Grid.ColumnSpan="2">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>

            <StackPanel Grid.Row="0" Orientation="Horizontal" 
                        HorizontalAlignment="Right" Margin="0,0,0,5">
                <Button Content="Старт" Width="100" Height="35" Margin="0,0,10,0"
                        Command="{Binding StartCommand}"
                        IsEnabled="{Binding CanStart}"
                        Background="#27AE60" Foreground="White"/>

                <Button Content="Стоп" Width="100" Height="35"
                        Command="{Binding StopCommand}"
                        IsEnabled="{Binding CanStop}"
                        Background="#E74C3C" Foreground="White"/>
            </StackPanel>

            <GroupBox Grid.Row="1" Header="Лог операций" Margin="0,0,0,0">
                <ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Auto" Height="120">
                    <TextBox Text="{Binding LogMessages, Mode=OneWay}" 
                             IsReadOnly="True" 
                             TextWrapping="Wrap"
                             FontFamily="Consolas"
                             FontSize="11"
                             Background="#F5F5F5"/>
                </ScrollViewer>
            </GroupBox>
        </Grid>

        <!-- Прогресс бар -->
        <Grid Grid.Row="7" Grid.ColumnSpan="2" Margin="0,5,0,0">
            <ProgressBar Value="{Binding ProgressValue}" 
                         Maximum="{Binding ProgressMaximum}"
                         IsIndeterminate="{Binding IsIndeterminate}"
                         Height="28"
                         MinHeight="28"/>
            <TextBlock Text="{Binding ProgressText}" 
                       HorizontalAlignment="Center" 
                       VerticalAlignment="Center"
                       FontSize="12"
                       FontWeight="Bold"
                       Foreground="#2C3E50"/>
        </Grid>
    </Grid>
</Window>
*/